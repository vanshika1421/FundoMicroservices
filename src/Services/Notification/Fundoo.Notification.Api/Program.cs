using Fundoo.Notification.Api.Services;
using Microsoft.Extensions.Options;
using FluentValidation;
using Fundoo.Notification.Api.Validation;
using Fundoo.Notification.Api.Services.Messaging;
using Fundoo.Notification.Application.DTO;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Register controllers
builder.Services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationActionFilter>();
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
builder.Services.AddScoped<FluentValidationActionFilter>();
builder.Services.AddValidatorsFromAssemblyContaining<PasswordResetNotificationRequestValidator>();

// Register Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks()
    .AddCheck("notification-api", () => HealthCheckResult.Healthy(), tags: new[] { "live", "ready" });
builder.Services.AddSingleton<SmtpBulkhead>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Fundoo Notification API",
            Version = "v1",
            Description = "API for sending Fundoo email notifications"
        });
});

// Register Notification Service
builder.Services.AddTransient<INotificationService, SmtpEmailService>();
builder.Services.AddSingleton<IKafkaDeadLetterPublisher, KafkaDeadLetterPublisher>();
builder.Services.AddHostedService<NoteEventsConsumer>();

var app = builder.Build();

// Configure Swagger
app.UseSwagger();

// The User API calls the local Notification API over HTTP during development.
// Keep HTTPS redirection for deployed environments, where certificates are configured.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

// Map controller routes
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();
