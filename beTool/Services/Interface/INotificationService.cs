namespace Services.Interface
{
    public interface INotificationService
    {
        // Lưu FCM token của user khi FE đăng ký
        Task SaveFcmTokenAsync(int userId, string fcmToken);

        // Gửi notification khi publish thành công
        Task NotifyPublishSuccessAsync(int userId, int postId, string platform);

        // Gửi notification khi publish thất bại
        Task NotifyPublishFailedAsync(int userId, int postId, string platform, string error);
    }
}