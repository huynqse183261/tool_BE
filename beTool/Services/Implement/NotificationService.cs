using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Repositories;
using Services.Interface;
using System.Text.Json;

namespace Services.Implement
{
    public class NotificationService : INotificationService
    {
        private readonly UserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public NotificationService(
            UserRepository userRepository,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _configuration = configuration;

            // Khởi tạo Firebase App 1 lần duy nhất
            if (FirebaseApp.DefaultInstance == null)
            {
                var serviceAccountJson = _configuration
                    .GetSection("Firebase:ServiceAccount")
                    .Get<Dictionary<string, object>>();

                var json = JsonSerializer.Serialize(serviceAccountJson);

                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential
                        .FromJson(json)
                        .CreateScoped("https://www.googleapis.com/auth/firebase.messaging")
                });
            }
        }


        public async Task NotifyPublishSuccessAsync(int userId, int postId, string platform)
        {
            var fcmToken = await GetFcmTokenAsync(userId);
            if (string.IsNullOrWhiteSpace(fcmToken)) return;

            var message = new Message
            {
                Token = fcmToken,
                Notification = new Notification
                {
                    Title = "Post Published ✅",
                    Body = $"Your post #{postId} has been published to {platform} successfully."
                },
                Data = new Dictionary<string, string>
                {
                    { "postId", postId.ToString() },
                    { "platform", platform },
                    { "type", "publish_success" }
                }
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);
        }

        public async Task NotifyPublishFailedAsync(int userId, int postId, string platform, string error)
        {
            var fcmToken = await GetFcmTokenAsync(userId);
            if (string.IsNullOrWhiteSpace(fcmToken)) return;

            var message = new Message
            {
                Token = fcmToken,
                Notification = new Notification
                {
                    Title = "Publish Failed ❌",
                    Body = $"Post #{postId} failed to publish to {platform}. Tap to view details."
                },
                Data = new Dictionary<string, string>
                {
                    { "postId", postId.ToString() },
                    { "platform", platform },
                    { "type", "publish_failed" },
                    { "error", error }
                }
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);
        }
        public async Task SaveFcmTokenAsync(int userId, string fcmToken)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return;

            // Cập nhật FCM token mới nhất của user
            user.FcmToken = fcmToken;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveAsync();
        }

        private async Task<string?> GetFcmTokenAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user?.FcmToken;
        }

    }
}