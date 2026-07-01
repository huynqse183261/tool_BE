using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.Enums
{
    public enum PostStatus
    {
        Draft = 1,
        Scheduled = 2,
        Publishing = 3,
        Published = 4,
        Failed = 5
    }
}