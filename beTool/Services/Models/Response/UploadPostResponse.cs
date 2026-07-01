using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.Response
{
    public class UploadPostResponse
    {
        public int PostId { get; set; }
        public List<string> ImageUrls { get; set; } = new List<string>();
    }
}