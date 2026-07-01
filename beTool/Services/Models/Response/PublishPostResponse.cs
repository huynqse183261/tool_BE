using System;

namespace Services.Models.Response
{
    public class PublishPostResponse
    {
        public int PostId { get; set; }
        public string FacebookPostId { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public string Caption { get; set; } = string.Empty;
    }
}
