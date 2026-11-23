using Fundoo.Notification.Application.DTO;
using Fundoo.Notification.Application.Templates;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Fundoo.Notification.Api.Services;

public sealed class SmtpEmailService(
    IConfiguration configuration,
    SmtpBulkhead bulkhead) : INotificationService
{
    public async Task SendNoteActivityNotificationAsync(
        NoteActivityNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = NoteActivityEmailTemplate.Build(
            request.Title, request.EventType, request.NoteId);
        await SendEmailAsync(
            request.RecipientEmail,
            "Fundoo Notes - Note activity",
            body,
            cancellationToken);
    }

    public async Task SendPasswordResetNotificationAsync(
    PasswordResetNotificationRequest request)
    {
        await bulkhead.ExecuteAsync(async () =>
        {
            var body = PasswordResetEmailTemplate.Build(
                "User",
                request.ResetToken);

            await SendEmailAsync(
                request.RecipientEmail,
                "Fundoo Notes - Password Reset",
                body);
        });
    }

    public async Task SendWelcomeNotificationAsync(
        WelcomeNotificationRequest request)
    {
        var body = WelcomeEmailTemplate.Build(
            request.RecipientName);

        await SendEmailAsync(
            request.RecipientEmail,
            "Welcome to Fundoo Notes!",
            body);
    }

    private async Task SendEmailAsync(
        string recipientEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var host = GetRequiredSetting("Host");

        if (!int.TryParse(
                GetRequiredSetting("Port"),
                out var port))
        {
            throw new InvalidOperationException(
                "EmailSettings:Port must be a valid number.");
        }

        var senderEmail = GetRequiredSetting("SenderEmail");
        var senderName = configuration["EmailSettings:SenderName"]
                         ?? "Fundoo Notes";

        var username = GetRequiredSetting("Username");
        var password = GetRequiredSetting("Password");

        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(senderName, senderEmail));

        message.To.Add(
            MailboxAddress.Parse(recipientEmail));

        message.Subject = subject;

        message.Body = new TextPart("html")
        {
            Text = body
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(
            host,
            port,
            SecureSocketOptions.StartTls,
            cancellationToken);

        try
        {
            await client.AuthenticateAsync(username, password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }

    public async Task SendTwoFactorOtpNotificationAsync(
        TwoFactorOtpNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        await bulkhead.ExecuteAsync(async () =>
        {
            var body = TwoFactorOtpEmailTemplate.Build(
                request.RecipientName,
                request.OtpCode,
                request.ExpiryMinutes);

            await SendEmailAsync(
                request.RecipientEmail,
                "Fundoo Notes - Two-Factor Authentication OTP",
                body,
                cancellationToken);
        });
    }


    private string GetRequiredSetting(string key)
    {
        return configuration[$"EmailSettings:{key}"]
               ?? throw new InvalidOperationException(
                   $"EmailSettings:{key} is missing.");
    }
}
