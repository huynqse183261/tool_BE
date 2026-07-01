namespace Services.Models.Response
{
    public class PostSummaryResponse
    {
        public int Id { get; set; }
        public string? ThumbnailUrl { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? FacebookPostId { get; set; }
        public List<PlatformSummary> Platforms { get; set; } = new();
    }

    public class PlatformSummary
    {
        public string Platform { get; set; } = "";
        public string? PlatformPostId { get; set; }
        public DateTime? PublishedAt { get; set; }
    }
}