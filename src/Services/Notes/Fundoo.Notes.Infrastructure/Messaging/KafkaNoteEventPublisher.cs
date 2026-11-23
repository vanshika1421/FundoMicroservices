using Confluent.Kafka;
using Fundoo.EventContracts.Events;
using Fundoo.Notes.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fundoo.Notes.Infrastructure.Messaging;

public sealed class KafkaNoteEventPublisher : INoteEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _createdTopic;
    private readonly string _updatedTopic;
    private readonly ILogger<KafkaNoteEventPublisher> _logger;

    public KafkaNoteEventPublisher(
        IConfiguration configuration,
        ILogger<KafkaNoteEventPublisher> logger)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
            throw new InvalidOperationException("Kafka:BootstrapServers is missing.");

        _createdTopic = configuration["Kafka:Topics:NoteCreated"]
            ?? throw new InvalidOperationException("Kafka:Topics:NoteCreated is missing.");
        _updatedTopic = configuration["Kafka:Topics:NoteUpdated"]
            ?? throw new InvalidOperationException("Kafka:Topics:NoteUpdated is missing.");
        _logger = logger;

        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            // Keep a transient broker outage from immediately failing a note request.
            MessageSendMaxRetries = 10,
            RetryBackoffMs = 500,
            MessageTimeoutMs = 120_000
        }).Build();
    }

    public Task PublishCreatedAsync(NoteEvent noteEvent, CancellationToken cancellationToken) =>
        PublishAsync(_createdTopic, noteEvent, cancellationToken);

    public Task PublishUpdatedAsync(NoteEvent noteEvent, CancellationToken cancellationToken) =>
        PublishAsync(_updatedTopic, noteEvent, cancellationToken);

    private async Task PublishAsync(
        string topic,
        NoteEvent noteEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = noteEvent.NoteId.ToString(),
                Value = JsonSerializer.Serialize(noteEvent),
                Headers = new Headers
                {
                    { "event-id", System.Text.Encoding.UTF8.GetBytes(noteEvent.EventId.ToString()) },
                    { "event-type", System.Text.Encoding.UTF8.GetBytes(noteEvent.EventType) },
                    { "event-version", System.Text.Encoding.UTF8.GetBytes(noteEvent.Version.ToString()) }
                }
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Failed publishing {EventType} for note {NoteId} to {Topic}",
                noteEvent.EventType, noteEvent.NoteId, topic);
            throw;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
