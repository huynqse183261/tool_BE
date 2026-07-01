using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.Response
{
    public class DraftSummaryResponse
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Caption { get; set; }
        public string? Status { get; set; }
        public string? ContentType { get; set; }
        public string? ThumbnailUrl { get; set; } // ảnh đầu tiên
        public DateTime? ScheduledAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
