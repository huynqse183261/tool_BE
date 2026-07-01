using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Repositories;
using Repositories.Entities;
using Services.Interface;
using Services.Models.Common;
using Services.Models.Enums;
using Services.Models.Request;
using Services.Models.Response;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
namespace Services.Implement
{
    public class PostService : IPostService
    {
        private readonly PostRepository _postRepository;
        private readonly PostImageRepository _postImageRepository;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly PostPlatformRepository _postPlatformRepository;
        private readonly ScheduledJobRepository _scheduledJobRepository;
        private readonly SocialAccountRepository _socialAccountRepository;
        private readonly PostPlatformRepository _postPlatformRepo;
        private readonly ScheduledJobRepository _scheduledJobRepo;
        private readonly SocialAccountRepository _socialAccountRepo;
        public PostService(
            PostRepository postRepository,
            PostImageRepository postImageRepository,
            PostPlatformRepository postPlatformRepository,
            ScheduledJobRepository scheduledJobRepository,
            SocialAccountRepository socialAccountRepository,
            ICloudinaryService cloudinaryService,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _postRepository = postRepository;
            _postImageRepository = postImageRepository;
            _postPlatformRepository = postPlatformRepository;
            _scheduledJobRepository = scheduledJobRepository;
            _socialAccountRepository = socialAccountRepository;
            _cloudinaryService = cloudinaryService;
            _configuration = configuration;
            _environment = environment;
        }
        // Inject thêm repositories cần thiết



        // Lấy danh sách drafts của user
        public async Task<ApiResponse<List<DraftSummaryResponse>>> GetDraftsAsync(int userId)
        {
            try
            {
                var draftList = await _postRepository.GetDraftsByUserAsync(userId);

                var responseList = draftList.Select(p => new DraftSummaryResponse
                {
                    Id = p.Id,
                    Title = p.Title,
                    Caption = p.Caption,
                    Status = p.Status,
                    ContentType = p.ContentType,
                    ThumbnailUrl = p.PostImages.FirstOrDefault()?.ImageUrl,
                    ScheduledAt = p.ScheduledAt,
                    UpdatedAt = (DateTime)p.UpdatedAt
                }).ToList();

                return new ApiResponse<List<DraftSummaryResponse>>
                {
                    Success = true,
                    Message = "Success",
                    Data = responseList
                };
            }
            catch (Exception ex)
            {
                // Catch unexpected errors from repository or mapping
                return new ApiResponse<List<DraftSummaryResponse>>
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        // Xóa draft — chỉ cho xóa khi status là Draft
        public async Task<ApiResponse<bool>> DeleteDraftAsync(int postId, int userId)
        {
            try
            {
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null)
                    return new ApiResponse<bool> { Success = false, Message = "Post not found", Data = false };

                if (post.UserId != userId)
                    return new ApiResponse<bool> { Success = false, Message = "Forbidden", Data = false };

                if (!string.Equals(post.Status, PostStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
                    return new ApiResponse<bool> { Success = false, Message = "Only drafts can be deleted", Data = false };

                await _postRepository.RemoveAsync(post);
                await _postRepository.SaveAsync();

                return new ApiResponse<bool> { Success = true, Message = "Draft deleted", Data = true };
            }
            catch (Exception ex)
            {
                // Catch delete errors (FK constraint, DB error...)
                return new ApiResponse<bool> { Success = false, Message = $"An error occurred: {ex.Message}", Data = false };
            }
        }

        // Đặt lịch đăng bài — tạo PostPlatform + ScheduledJob, set status Scheduled
        public async Task<ApiResponse<bool>> SchedulePostAsync(int postId, int userId, SchedulePostRequest request)
        {
            try
            {
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null)
                    return new ApiResponse<bool> { Success = false, Message = "Post not found", Data = false };

                if (post.UserId != userId)
                    return new ApiResponse<bool> { Success = false, Message = "Forbidden", Data = false };

                if (!string.Equals(post.Status, PostStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
                    return new ApiResponse<bool> { Success = false, Message = "Only drafts can be scheduled", Data = false };

                if (request.ScheduledAt <= DateTime.UtcNow)
                    return new ApiResponse<bool> { Success = false, Message = "ScheduledAt must be in the future", Data = false };

                if (request.Platforms == null || request.Platforms.Count == 0)
                    return new ApiResponse<bool> { Success = false, Message = "At least one platform required", Data = false };

                // Tạo PostPlatform cho từng platform được chọn
                foreach (var platform in request.Platforms)
                {
                    // Lấy social account tương ứng của user cho platform này
                    var socialAccount = await _socialAccountRepository.GetActiveByUserAndPlatformAsync(userId, platform);

                    var postPlatform = new PostPlatform
                    {
                        PostId = postId,
                        Platform = platform,
                        Status = "Pending",
                        SocialAccountId = socialAccount?.Id
                    };

                    await _postPlatformRepository.CreateAsync(postPlatform);
                }

                // Tạo ScheduledJob để Hangfire pick up
                var scheduledJob = new ScheduledJob
                {
                    PostId = postId,
                    JobType = "PublishPost",
                    ExecuteAt = request.ScheduledAt,
                    Status = "Pending",
                    RetryCount = 0,
                    CreatedAt = DateTime.UtcNow
                };

                await _scheduledJobRepository.CreateAsync(scheduledJob);

                // Update post status và scheduled time
                post.Status = PostStatus.Scheduled.ToString();
                post.ScheduledAt = request.ScheduledAt;
                post.UpdatedAt = DateTime.UtcNow;

                await _postRepository.UpdateAsync(post);
                await _postPlatformRepository.SaveAsync();
                await _scheduledJobRepository.SaveAsync();
                await _postRepository.SaveAsync();
                BackgroundJob.Schedule<IPublishingJob>(
    job => job.ExecuteAsync(postId),
    request.ScheduledAt - DateTime.UtcNow
);

                return new ApiResponse<bool> { Success = true, Message = "Post scheduled successfully", Data = true };
            }
            catch (Exception ex)
            {
                // Catch scheduling errors (DB, FK, validation...)
                return new ApiResponse<bool> { Success = false, Message = $"An error occurred: {ex.Message}", Data = false };
            }

        }

        // Hủy lịch — reset về Draft, xóa ScheduledJob + PostPlatforms pending
        public async Task<ApiResponse<bool>> CancelScheduleAsync(int postId, int userId)
        {
            try
            {
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null)
                    return new ApiResponse<bool> { Success = false, Message = "Post not found", Data = false };

                if (post.UserId != userId)
                    return new ApiResponse<bool> { Success = false, Message = "Forbidden", Data = false };

                if (!string.Equals(post.Status, PostStatus.Scheduled.ToString(), StringComparison.OrdinalIgnoreCase))
                    return new ApiResponse<bool> { Success = false, Message = "Post is not scheduled", Data = false };

                // Xóa pending PostPlatforms (chưa publish)
                await _postPlatformRepository.DeletePendingByPostAsync(postId);

                // Xóa pending ScheduledJob
                await _scheduledJobRepository.DeletePendingByPostAsync(postId);

                // Reset post về Draft
                post.Status = PostStatus.Draft.ToString();
                post.ScheduledAt = null;
                post.UpdatedAt = DateTime.UtcNow;

                await _postRepository.UpdateAsync(post);
                await _postRepository.SaveAsync();

                return new ApiResponse<bool> { Success = true, Message = "Schedule cancelled", Data = true };
            }
            catch (Exception ex)
            {
                // Catch cancel errors
                return new ApiResponse<bool> { Success = false, Message = $"An error occurred: {ex.Message}", Data = false };
            }
        }
        public async Task<ApiResponse<UploadPostResponse>> UploadPostAsync(UploadPostRequest request, int userId)
        {
            try
            {
                // Validate files
                if (request.Images == null || request.Images.Count == 0)
                {
                    return new ApiResponse<UploadPostResponse>
                    {
                        Success = false,
                        Message = "No image files provided"
                    };
                }

                // Validate file count (max 10 images per post)
                const int maxImages = 10;
                if (request.Images.Count > maxImages)
                {
                    return new ApiResponse<UploadPostResponse>
                    {
                        Success = false,
                        Message = $"Maximum {maxImages} images allowed per post"
                    };
                }

                // Upload all images to Cloudinary
                var uploadedUrls = await _cloudinaryService.UploadImagesAsync(request.Images);

                if (uploadedUrls.Count == 0)
                {
                    return new ApiResponse<UploadPostResponse>
                    {
                        Success = false,
                        Message = "Failed to upload any images"
                    };
                }

                // Create Post entity
                var post = new Post
                {
                    UserId = userId,
                    Title = request.Title ?? string.Empty,
                    Caption = request.Caption ?? string.Empty,
                    Status = PostStatus.Draft.ToString(), // Default status
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Save post to database
                await _postRepository.CreateAsync(post);
                await _postRepository.SaveAsync();

                // Create PostImage entities for each uploaded image
                var postImages = new List<PostImage>();
                for (int i = 0; i < uploadedUrls.Count; i++)
                {
                    var postImage = new PostImage
                    {
                        PostId = post.Id,
                        ImageUrl = uploadedUrls[i], // Cloudinary URL
                        DisplayOrder = i + 1,
                        CreatedAt = DateTime.UtcNow
                    };
                    postImages.Add(postImage);
                }

                // Save all post images to database
                foreach (var postImage in postImages)
                {
                    await _postImageRepository.CreateAsync(postImage);
                }
                await _postImageRepository.SaveAsync();

                // Prepare response
                var response = new UploadPostResponse
                {
                    PostId = post.Id,
                    ImageUrls = uploadedUrls
                };

                return new ApiResponse<UploadPostResponse>
                {
                    Success = true,
                    Message = $"Post created successfully with {uploadedUrls.Count} image(s)",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<UploadPostResponse>
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<PostDetailResponse>> GetPostDetailAsync(int postId, int userId)
        {
            try
            {
                var post = await _postRepository.GetPostWithImagesAsync(postId);
                if (post == null)
                {
                    return new ApiResponse<PostDetailResponse>
                    {
                        Success = false,
                        Message = "Post not found"
                    };
                }

                if (post.UserId != userId)
                {
                    return new ApiResponse<PostDetailResponse>
                    {
                        Success = false,
                        Message = "Forbidden"
                    };
                }

                var data = new PostDetailResponse
                {
                    Id = post.Id,
                    Title = post.Title,
                    Caption = post.Caption,
                    Hashtags = post.Hashtags,
                    Status = post.Status,
                    FacebookPostId = post.FacebookPostId,
                    ScheduledAt = post.ScheduledAt,
                    PublishedAt = post.PublishedAt,
                    ContentType = post.ContentType,
                    SceneType = post.SceneType,
                    MoodType = post.MoodType,
                    Images = post.PostImages
                        .OrderBy(i => i.DisplayOrder)
                        .Select(i => new PostImageDto
                        {
                            Id = i.Id,
                            ImageUrl = i.ImageUrl,
                            DisplayOrder = i.DisplayOrder
                        })
                        .ToList()
                };

                return new ApiResponse<PostDetailResponse>
                {
                    Success = true,
                    Message = "Success",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<PostDetailResponse>
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<bool>> UpdateDraftAsync(int postId, int userId, UpdatePostRequest request)
        {
            try
            {
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Post not found",
                        Data = false
                    };
                }

                if (post.UserId != userId)
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Forbidden",
                        Data = false
                    };
                }

                // Only allow editing drafts/scheduled (safe for now)
                if (string.Equals(post.Status, PostStatus.Published.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Cannot edit published post",
                        Data = false
                    };
                }

                if (request.Title != null) post.Title = request.Title;
                if (request.Caption != null) post.Caption = request.Caption;
                if (request.Hashtags != null) post.Hashtags = request.Hashtags;

                // Optional metadata for editor
                if (request.ContentType != null) post.ContentType = request.ContentType;
                if (request.SceneType != null) post.SceneType = request.SceneType;
                if (request.MoodType != null) post.MoodType = request.MoodType;

                post.ScheduledAt = request.ScheduledAt;
                post.UpdatedAt = DateTime.UtcNow;
                post.EditedByUser = true;

                // Auto status update when scheduledAt is set
                if (request.ScheduledAt.HasValue)
                {
                    post.Status = PostStatus.Scheduled.ToString();
                }
                else if (string.IsNullOrEmpty(post.Status))
                {
                    post.Status = PostStatus.Draft.ToString();
                }

                await _postRepository.UpdateAsync(post);
                await _postRepository.SaveAsync();

                return new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Draft updated successfully",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}",
                    Data = false
                };
            }
        }
        public async Task<ApiResponse<List<PostSummaryResponse>>> GetPublishedPostsAsync(int userId)
        {
            try
            {
                var publishedPosts = await _postRepository.GetPublishedByUserAsync(userId);

                var responseList = publishedPosts.Select(p => new PostSummaryResponse
                {
                    Id = p.Id,
                    ThumbnailUrl = p.PostImages.FirstOrDefault()?.ImageUrl,
                    PublishedAt = p.PublishedAt,
                    FacebookPostId = p.FacebookPostId,
                    Platforms = p.PostPlatforms
                        .Where(pp => pp.Status == "Published")
                        .Select(pp => new PlatformSummary
                        {
                            Platform = pp.Platform,
                            PlatformPostId = pp.PlatformPostId,
                            PublishedAt = pp.PublishedAt
                        }).ToList()
                }).ToList();

                return new ApiResponse<List<PostSummaryResponse>>
                {
                    Success = true,
                    Message = "Success",
                    Data = responseList
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<List<PostSummaryResponse>>
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}"
                };
            }
        }
    }
}
