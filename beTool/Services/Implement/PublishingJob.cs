using Hangfire;
using Repositories;
using Services.Interface;
using Services.Models.Enums;
using Services.Models.Request;

namespace Services.Implement
{
    // AutomaticRetry: Hangfire tự retry 3 lần nếu job throw exception
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public class PublishingJob : IPublishingJob
    {
        private readonly PostRepository _postRepository;
        private readonly PostPlatformRepository _postPlatformRepository;
        private readonly ScheduledJobRepository _scheduledJobRepository;
        private readonly IPublishService _publishService;


        public PublishingJob(
            PostRepository postRepository,
            PostPlatformRepository postPlatformRepository,
            ScheduledJobRepository scheduledJobRepository,
            IPublishService publishService
)
        {
            _postRepository = postRepository;
            _postPlatformRepository = postPlatformRepository;
            _scheduledJobRepository = scheduledJobRepository;
            _publishService = publishService;
        }

        public async Task ExecuteAsync(int postId)
        {
            // Load ScheduledJob để update RetryCount/LastError
            var scheduledJob = await _scheduledJobRepository.GetPendingByPostAsync(postId);

            try
            {
                var post = await _postRepository.GetPostForPublishingAsync(postId);
                if (post == null)
                    throw new Exception($"Post {postId} not found");

                // Set Publishing trước khi bắt đầu — tránh duplicate job chạy song song
                post.Status = PostStatus.Publishing.ToString();
                post.UpdatedAt = DateTime.UtcNow;
                await _postRepository.UpdateAsync(post);
                await _postRepository.SaveAsync();

                // Loop qua từng platform cần publish
                var platforms = await _postPlatformRepository.GetByPostIdAsync(postId);
                var hasError = false;
                foreach (var platform in platforms.Where(p => p.Status == "Pending"))
                {
                    try
                    {
                        if (platform.Platform == "Facebook")
                        {
                            // Log caption để verify không null trước khi publish
                            Console.WriteLine($"[PublishingJob] Post {postId} caption: '{post.Caption}'");

                            var result = await _publishService.PublishToFacebookAsync(
                                postId, post.UserId, new PublishPostRequest
                                {
                                    Caption = post.Caption
                                });

                            if (!result.Success)
                            {
                                Console.WriteLine($"[PublishingJob] Failed post {postId}: {result.Message}");
                                platform.Status = "Failed";
                                platform.ErrorMessage = result.Message;
                                hasError = true;
                            }
                            else
                            {
                                // Success — không set hasError
                                platform.Status = "Published";
                                platform.PlatformPostId = result.Data?.FacebookPostId;
                                platform.PublishedAt = DateTime.UtcNow;
                            }
                        }
                    }
                    catch (Exception platformEx)
                    {
                        Console.WriteLine($"[PublishingJob] Exception on platform {platform.Platform}: {platformEx.Message}");
                        platform.Status = "Failed";
                        platform.ErrorMessage = platformEx.Message;
                        hasError = true;
                    }

                    await _postPlatformRepository.UpdateAsync(platform);
                }

                await _postPlatformRepository.SaveAsync();

                // Set Post.Status cuối cùng dựa trên kết quả các platforms
                post.Status = hasError
                    ? PostStatus.Failed.ToString()
                    : PostStatus.Published.ToString();

                post.UpdatedAt = DateTime.UtcNow;
                await _postRepository.UpdateAsync(post);
                await _postRepository.SaveAsync();

                // Update ScheduledJob status
                if (scheduledJob != null)
                {
                    scheduledJob.Status = hasError ? "Failed" : "Completed";
                    await _scheduledJobRepository.UpdateAsync(scheduledJob);
                    await _scheduledJobRepository.SaveAsync();
                }
            }
            catch (Exception ex)
            {
                // Catch toàn bộ job fail — Hangfire sẽ tự retry theo [AutomaticRetry]
                if (scheduledJob != null)
                {
                    scheduledJob.RetryCount += 1;
                    scheduledJob.LastError = ex.Message;
                    scheduledJob.Status = "Failed";
                    await _scheduledJobRepository.UpdateAsync(scheduledJob);
                    await _scheduledJobRepository.SaveAsync();
                }

                // Re-throw để Hangfire biết job fail và trigger retry
                throw;
            }
        }
    }
}