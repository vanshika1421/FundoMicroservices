namespace Fundoo.Notification.Application.DTO
{
    public interface INotificationService
    {
        Task SendPasswordResetNotificationAsync(
            PasswordResetNotificationRequest request);

        Task SendWelcomeNotificationAsync(
            WelcomeNotificationRequest request);

        Task SendTwoFactorOtpNotificationAsync(
            TwoFactorOtpNotificationRequest request,
            CancellationToken cancellationToken = default);

        Task SendNoteActivityNotificationAsync(
            NoteActivityNotificationRequest request,
            CancellationToken cancellationToken = default);
    }
}
