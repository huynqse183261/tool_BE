using Services.Models.Enums;
using System.Collections.Generic;

namespace Services.Models.Response
{
    public class GenerateCaptionResponse
    {
        public int PostId { get; set; }
        public string Caption { get; set; } = string.Empty;
        public string Analysis { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        
        // Brand-aware fields
        public string? ContentType { get; set; }
        public string? Tone { get; set; }
        public string? Title { get; set; }
        public List<string>? Hashtags { get; set; }
    }
}