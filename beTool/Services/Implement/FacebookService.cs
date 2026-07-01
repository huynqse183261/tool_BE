using Services.Interface;
using System.Text.Json;

namespace Services.Implement
{
    public class FacebookService : IFacebookService
    {
        private const string GraphBaseUrl = "https://graph.facebook.com/v19.0";
        private readonly HttpClient _httpClient;

        public FacebookService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Upload 1 ảnh unpublished — trả về photo_id để attach vào feed
        public async Task<string> UploadPhotoAsync(string imageUrl, string pageId, string pageToken)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("Image URL is required", nameof(imageUrl));

            var url = $"{GraphBaseUrl}/{pageId}/photos";

            using var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("url", imageUrl),
                new KeyValuePair<string, string>("published", "false"),
                new KeyValuePair<string, string>("access_token", pageToken)
            });

            var response = await _httpClient.PostAsync(url, formData);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Facebook upload photo failed: {body}");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("id", out var idProp))
                throw new Exception($"Facebook upload photo response missing id. Body: {body}");

            return idProp.GetString() ?? throw new Exception("Facebook photo id is empty");
        }

        // Publish post với 1 ảnh đã upload
        public async Task<string> PublishPostAsync(string caption, string photoId, string pageId, string pageToken)
        {
            if (string.IsNullOrWhiteSpace(caption))
                throw new ArgumentException("Caption is required", nameof(caption));

            var url = $"{GraphBaseUrl}/{pageId}/feed";

            var attachedMedia = JsonSerializer.Serialize(new[]
            {
                new { media_fbid = photoId }
            });

            using var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", caption),
                new KeyValuePair<string, string>("attached_media", attachedMedia),
                new KeyValuePair<string, string>("access_token", pageToken)
            });

            var response = await _httpClient.PostAsync(url, formData);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Facebook publish post failed: {body}");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("id", out var idProp))
                throw new Exception($"Facebook publish response missing id. Body: {body}");

            return idProp.GetString() ?? throw new Exception("Facebook post id is empty");
        }

        // Publish carousel — upload từng ảnh song song rồi attach tất cả vào 1 feed post
        public async Task<string> PublishCarouselPostAsync(string caption, List<string> imageUrls, string pageId, string pageToken)
        {
            if (imageUrls == null || imageUrls.Count == 0)
                throw new ArgumentException("At least one image URL is required", nameof(imageUrls));

            // Upload tất cả ảnh song song để tiết kiệm thời gian
            var uploadTasks = imageUrls.Select(url => UploadPhotoAsync(url, pageId, pageToken));
            var photoIds = await Task.WhenAll(uploadTasks);

            // Build attached_media array từ tất cả photo_ids
            var attachedMedia = JsonSerializer.Serialize(
                photoIds.Select(id => new { media_fbid = id }).ToArray()
            );

            var feedUrl = $"{GraphBaseUrl}/{pageId}/feed";

            using var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", caption),
                new KeyValuePair<string, string>("attached_media", attachedMedia),
                new KeyValuePair<string, string>("access_token", pageToken)
            });

            var response = await _httpClient.PostAsync(feedUrl, formData);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Facebook carousel publish failed: {body}");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("id", out var idProp))
                throw new Exception($"Facebook carousel response missing id. Body: {body}");

            return idProp.GetString() ?? throw new Exception("Facebook carousel post id is empty");
        }
        public async Task<string> PublishVideoPostAsync(string caption, string videoUrl, string pageId, string pageToken)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
                throw new ArgumentException("Video URL is required", nameof(videoUrl));

            var url = $"{GraphBaseUrl}/{pageId}/videos";

            using var formData = new FormUrlEncodedContent(new[]
            {
        new KeyValuePair<string, string>("file_url", videoUrl),
        new KeyValuePair<string, string>("description", caption),
        new KeyValuePair<string, string>("access_token", pageToken)
    });

            var response = await _httpClient.PostAsync(url, formData);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Facebook video publish error: {body}");
                throw new Exception($"Facebook publish video failed: {body}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("id", out var idProp))
                throw new Exception($"Facebook video response missing id. Body: {body}");

            return idProp.GetString() ?? throw new Exception("Facebook video post id is empty");
        }
    }
}