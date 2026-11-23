using Fundoo.Notification.Application.DTO;
using Microsoft.AspNetCore.Mvc;

namespace Fundoo.Notification.Api.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    public sealed class NotificationController(INotificationService notificationService) : ControllerBase
    {
        private readonly INotificationService _notificationService = notificationService;

        [HttpPost("password-reset")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendPasswordReset(
      [FromBody] PasswordResetNotificationRequest request,
      CancellationToken cancellationToken)
        {
            // TEMPORARY: simulate a slow Notification API.
         //   await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);

            await _notificationService.SendPasswordResetNotificationAsync(request);

            return Accepted(new
            {
                isSuccess = true,
                message = "Password-reset notification accepted."
            });
        }
        [HttpPost("2fa-otp")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SendTwoFactorOtp(
    [FromBody] TwoFactorOtpNotificationRequest request,
    CancellationToken cancellationToken)
        {
            await _notificationService.SendTwoFactorOtpNotificationAsync(request, cancellationToken);

            return Accepted(new
            {
                isSuccess = true,
                message = "Two-factor authentication OTP notification accepted."
            });
        }

        [HttpPost("welcome")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SendWelcome([FromBody] WelcomeNotificationRequest request)
        {
            await _notificationService.SendWelcomeNotificationAsync(request);

            return Accepted(new
            {
                isSuccess = true,
                message = "Welcome notification accepted."
            });
        }
    }
}
