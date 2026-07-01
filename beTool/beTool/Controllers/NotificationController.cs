using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interface;
using System.Security.Claims;

namespace beTool.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpPost("fcm-token")]
        public async Task<IActionResult> SaveFcmToken([FromBody] SaveFcmTokenRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.FcmToken))
                return BadRequest(new { message = "FCM token is required" });

            await _notificationService.SaveFcmTokenAsync(userId, request.FcmToken);
            return Ok(new { message = "FCM token saved" });
        }
    }

    public class SaveFcmTokenRequest
    {
        public string FcmToken { get; set; } = "";
    }
}