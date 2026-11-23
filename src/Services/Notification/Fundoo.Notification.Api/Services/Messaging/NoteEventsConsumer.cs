using Confluent.Kafka;
using Fundoo.EventContracts.Events;
using Fundoo.Notification.Application.DTO;
using System.Text.Json;

namespace Fundoo.Notification.Api.Services.Messaging;

public sealed class NoteEventsConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    IKafkaDeadLetterPublisher deadLetterPublisher,
    ILogger<NoteEventsConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Consume() is synchronous and may wait indefinitely for a Kafka record.
        // Yield first so BackgroundService startup does not block Kestrel from binding its ports.
        await Task.Yield();

        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        var groupId = configuration["Kafka:ConsumerGroupId"];
        var topics = new[]
        {
            configuration["Kafka:Topics:NoteCreated"],
            configuration["Kafka:Topics:NoteUpdated"]
        };

        if (string.IsNullOrWhiteSpace(bootstrapServers) ||
            string.IsNullOrWhiteSpace(groupId) || topics.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Kafka consumer configuration is incomplete.");

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        }).Build();

        consumer.Subscribe(topics!);
        logger.LogInformation("Kafka notification consumer subscribed to {Topics}", string.Join(", ", topics));

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result = null;
            try
            {
                result = consumer.Consume(stoppingToken);
                if (string.IsNullOrWhiteSpace(result.Message.Value))
                    throw new InvalidDataException("Kafka note event has an empty payload.");

                var noteEvent = JsonSerializer.Deserialize<NoteEvent>(
                    result.Message.Value,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var expectedEventType = result.Topic == configuration["Kafka:Topics:NoteCreated"]
                    ? "notes.created.v1"
                    : "notes.updated.v1";
                if (noteEvent is null ||
                    noteEvent.EventId == Guid.Empty ||
                    noteEvent.NoteId == Guid.Empty ||
                    noteEvent.UserId == Guid.Empty ||
                    noteEvent.Version != 1 ||
                    !string.Equals(noteEvent.EventType, expectedEventType, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(noteEvent.RecipientEmail) ||
                    string.IsNullOrWhiteSpace(noteEvent.Title))
                    throw new InvalidDataException("Note event is missing required data.");

                using var scope = scopeFactory.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notificationService.SendNoteActivityNotificationAsync(
                    new NoteActivityNotificationRequest(
                        noteEvent.RecipientEmail,
                        noteEvent.NoteId,
                        noteEvent.Title,
                        noteEvent.EventType),
                    stoppingToken);

                consumer.Commit(result);
                logger.LogInformation(
                    "Processed {EventType} notification for note {NoteId} from {TopicPartitionOffset}",
                    noteEvent.EventType,
                    noteEvent.NoteId,
                    result.TopicPartitionOffset);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                if (result is not null &&
                    (exception is JsonException or InvalidDataException))
                {
                    try
                    {
                        await deadLetterPublisher.PublishAsync(result, stoppingToken);
                        consumer.Commit(result);
                        logger.LogWarning(
                            "Malformed Kafka note event at {TopicPartitionOffset} was moved to the dead-letter topic. Reason: {Reason}",
                            result.TopicPartitionOffset,
                            exception.Message);
                    }
                    catch (Exception deadLetterException)
                    {
                        logger.LogError(deadLetterException,
                            "Could not dead-letter malformed Kafka event; retrying the original record.");
                        consumer.Seek(result.TopicPartitionOffset);
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    }
                }
                else
                {
                    logger.LogError(exception, "Failed to process Kafka note event. The record will be retried.");
                    if (result is not null)
                        consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
        }

        consumer.Close();
    }
}
