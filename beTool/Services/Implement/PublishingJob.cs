using Hangfire;
using Repositories;
using Services.Interface;
using Services.Models.Enums;

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

        private readonly INotificationService _notificationService;

        public PublishingJob(
            PostRepository postRepository,
            PostPlatformRepository postPlatformRepository,
            ScheduledJobRepository scheduledJobRepository,
            IPublishService publishService,
            INotificationService notificationService)
        {
            _postRepository = postRepository;
            _postPlatformRepository = postPlatformRepository;
            _scheduledJobRepository = scheduledJobRepository;
            _publishService = publishService;
            _notificationService = notificationService;
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
                            // Gọi PublishService — reuse logic đã có
                            var result = await _publishService.PublishToFacebookAsync(
                                postId, post.UserId, null);

                            if (!result.Success)
                            {
                                platform.Status = "Failed";
                                platform.ErrorMessage = result.Message;
                                hasError = true;

                                // Notify thất bại
                                await _notificationService.NotifyPublishFailedAsync(
                                    post.UserId, postId, platform.Platform, result.Message);
                            }
                            else
                            {
                                // Notify thành công
                                await _notificationService.NotifyPublishSuccessAsync(
                                    post.UserId, postId, platform.Platform);
                            }
                        }
                        // Instagram sẽ thêm sau khi có token
                    }
                    catch (Exception platformEx)
                    {
                        // Catch lỗi từng platform riêng — không để 1 platform fail làm dừng cả job
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