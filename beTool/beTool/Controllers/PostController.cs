using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interface;
using Services.Models.Request;
using System.Security.Claims;

namespace beTool.Controllers
{
    [ApiController]
    [Route("api/posts")]
    [Authorize]
    public class PostController : Controller
    {
        private readonly IPostService _postService;
        private readonly IAiGenerationService _aiGenerationService;
        private readonly IPublishService _publishService;

        public PostController(
            IPostService postService,
            IAiGenerationService aiGenerationService,
            IPublishService publishService)
        {
            _postService = postService;
            _aiGenerationService = aiGenerationService;
            _publishService = publishService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadPost([FromForm] UploadPostRequest request)
        {
            // Get user ID from JWT token claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _postService.UploadPostAsync(request, userId);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPostDetail(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _postService.GetPostDetailAsync(id, userId);
            if (!result.Success)
            {
                // Forbidden vs not found already mapped in Message; keep simple
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPut("{id}")]
        [HttpPut("{id}/draft")]
        public async Task<IActionResult> UpdateDraft(int id, [FromBody] UpdatePostRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _postService.UpdateDraftAsync(id, userId, request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("{id}/generate-caption")]
        [HttpPost("{id}/generate")]
        public async Task<IActionResult> GenerateCaption(int id, [FromBody] GenerateCaptionRequest request)
        {
            // Get user ID from JWT token claims (for authorization)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _aiGenerationService.GenerateCaptionAsync(id, request);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("{id}/regenerate")]
        public async Task<IActionResult> RegenerateCaption(int id, [FromBody] RegenerateCaptionRequest request)
        {
            // Get user ID from JWT token claims (for authorization)
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _aiGenerationService.RegenerateCaptionAsync(id);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }

        [HttpPost("{id}/publish")]
        public async Task<IActionResult> Publish(int id, [FromBody] PublishPostRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _publishService.PublishToFacebookAsync(id, userId, request);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetDrafts()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _postService.GetDraftsAsync(userId);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDraft(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _postService.DeleteDraftAsync(id, userId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("{id}/schedule")]
        public async Task<IActionResult> SchedulePost(int id, [FromBody] SchedulePostRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _postService.SchedulePostAsync(id, userId, request);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpDelete("{id}/schedule")]
        public async Task<IActionResult> CancelSchedule(int id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _postService.CancelScheduleAsync(id, userId);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("published")]
        public async Task<IActionResult> GetPublishedPosts()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _postService.GetPublishedPostsAsync(userId);
            return Ok(result);
        }
        [HttpPost("upload-video")]
        public async Task<IActionResult> UploadVideo([FromForm] UploadVideoRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _postService.UploadVideoPostAsync(request, userId);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("{id}/publish-video")]
        public async Task<IActionResult> PublishVideo(int id, [FromBody] PublishPostRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _postService.PublishVideoToFacebookAsync(id, userId, request);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}
