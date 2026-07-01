using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.Request
{
    public class SchedulePostRequest
    {
        public DateTime ScheduledAt { get; set; }

        // Danh sách platform muốn publish: "Facebook", "Instagram"
        public List<string> Platforms { get; set; } = new();
    }
}
