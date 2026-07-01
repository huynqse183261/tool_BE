using System;

namespace Services.Models.Request
{
    public class UpdatePostRequest
    {
        public string? Title { get; set; }
        public string? Caption { get; set; }
        public string? Hashtags { get; set; }
        public DateTime? ScheduledAt { get; set; }

        // Optional: allow client to override AI metadata later
        public string? ContentType { get; set; }
        public string? SceneType { get; set; }
        public string? MoodType { get; set; }
    }
}
