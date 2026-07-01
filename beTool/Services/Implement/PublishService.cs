using Repositories;
using Services.Interface;
using Services.Models.Common;
using Services.Models.Enums;
using Services.Models.Request;
using Services.Models.Response;

namespace Services.Implement
{
    public class PublishService : IPublishService
    {
        private readonly PostRepository _postRepository;
        private readonly PostPlatformRepository _postPlatformRepository;
        private readonly SocialAccountRepository _socialAccountRepository;
        private readonly IFacebookService _facebookService;

        public PublishService(
            PostRepository postRepository,
            PostPlatformRepository postPlatformRepository,
            SocialAccountRepository socialAccountRepository,
            IFacebookService facebookService)
        {
            _postRepository = postRepository;
            _postPlatformRepository = postPlatformRepository;
            _socialAccountRepository = socialAccountRepository;
            _facebookService = facebookService;
        }

        public async Task<ApiResponse<PublishPostResponse>> PublishToFacebookAsync(int postId, int userId, PublishPostRequest request)
        {
            try
            {
                // Load post kèm images và platforms
                var post = await _postRepository.GetPostForPublishingAsync(postId);
                if (post == null)
                    return Fail<PublishPostResponse>("Post not found");

                if (post.UserId != userId)
                    return Fail<PublishPostResponse>("Forbidden");

                if (string.Equals(post.Status, PostStatus.Published.ToString(), StringComparison.OrdinalIgnoreCase))
                    return Fail<PublishPostResponse>("Post already published");

                // Lấy caption — ưu tiên request override, fallback về post.Caption
                var finalCaption = !string.IsNullOrWhiteSpace(request?.Caption)
                    ? request.Caption.Trim()
                    : post.Caption;

                if (string.IsNullOrWhiteSpace(finalCaption))
                    return Fail<PublishPostResponse>("Caption is empty. Generate or save a caption first.");

                // Lấy tất cả image URLs theo display order
                var imageUrls = post.PostImages
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => i.ImageUrl)
                    .ToList();

                if (imageUrls.Count == 0)
                    return Fail<PublishPostResponse>("No images found for this post");

                // Lấy social account Facebook của user từ DB — token động, không hardcode
                var socialAccount = await _socialAccountRepository
                    .GetActiveByUserAndPlatformAsync(userId, "Facebook");

                if (socialAccount == null)
                    return Fail<PublishPostResponse>("No active Facebook account connected");

                var pageId = socialAccount.PageId;
                var pageToken = socialAccount.AccessToken;

                if (string.IsNullOrWhiteSpace(pageId) || string.IsNullOrWhiteSpace(pageToken))
                    return Fail<PublishPostResponse>("Facebook Page ID or token is missing");

                // Publish carousel nếu nhiều ảnh, single photo nếu 1 ảnh
                string facebookPostId;
                if (imageUrls.Count == 1)
                {
                    var photoId = await _facebookService.UploadPhotoAsync(imageUrls[0], pageId, pageToken);
                    facebookPostId = await _facebookService.PublishPostAsync(finalCaption, photoId, pageId, pageToken);
                }
                else
                {
                    facebookPostId = await _facebookService.PublishCarouselPostAsync(finalCaption, imageUrls, pageId, pageToken);
                }

                var publishedAt = DateTime.UtcNow;

                // Update post status
                post.Caption = finalCaption;
                post.Status = PostStatus.Published.ToString();
                post.PublishedAt = publishedAt;
                post.FacebookPostId = facebookPostId;
                post.UpdatedAt = publishedAt;

                // Update PostPlatform status cho Facebook
                var facebookPlatform = post.PostPlatforms
                    .FirstOrDefault(pp => pp.Platform == "Facebook");

                if (facebookPlatform != null)
                {
                    facebookPlatform.Status = "Published";
                    facebookPlatform.PlatformPostId = facebookPostId;
                    facebookPlatform.PublishedAt = publishedAt;
                    await _postPlatformRepository.UpdateAsync(facebookPlatform);
                }

                await _postRepository.UpdateAsync(post);
                await _postRepository.SaveAsync();
                await _postPlatformRepository.SaveAsync();

                return new ApiResponse<PublishPostResponse>
                {
                    Success = true,
                    Message = "Published to Facebook successfully",
                    Data = new PublishPostResponse
                    {
                        PostId = postId,
                        FacebookPostId = facebookPostId,
                        PublishedAt = publishedAt,
                        Caption = finalCaption
                    }
                };
            }
            catch (Exception ex)
            {
                // Catch Graph API errors, network errors, token errors
                return Fail<PublishPostResponse>($"Publish failed: {ex.Message}");
            }
        }

        private static ApiResponse<T> Fail<T>(string message) =>
            new ApiResponse<T> { Success = false, Message = message };
    }
}