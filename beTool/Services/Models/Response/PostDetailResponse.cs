using System;
using System.Collections.Generic;

namespace Services.Models.Response
{
    public class PostDetailResponse
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Caption { get; set; }
        public string? Hashtags { get; set; }
        public string? Status { get; set; }
        public string? FacebookPostId { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? PublishedAt { get; set; }

        public string? ContentType { get; set; }
        public string? SceneType { get; set; }
        public string? MoodType { get; set; }
        public string? PostType { get; set; }
        public string? VideoUrl { get; set; }

        public List<PostImageDto> Images { get; set; } = new();
    }

    public class PostImageDto
    {
        public int Id { get; set; }
        public string? ImageUrl { get; set; }
        public int? DisplayOrder { get; set; }
    }
}
