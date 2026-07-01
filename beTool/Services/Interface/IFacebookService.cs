namespace Services.Interface
{
    public interface IFacebookService
    {
        // Upload 1 ảnh lên Page (unpublished) — trả về photo_id
        Task<string> UploadPhotoAsync(string imageUrl, string pageId, string pageToken);

        // Publish post với 1 ảnh
        Task<string> PublishPostAsync(string caption, string photoId, string pageId, string pageToken);

        // Publish carousel post với nhiều ảnh — upload từng ảnh rồi attach vào feed
        Task<string> PublishCarouselPostAsync(string caption, List<string> imageUrls, string pageId, string pageToken);
    }
}