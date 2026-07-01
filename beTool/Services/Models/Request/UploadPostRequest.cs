using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.Request
{
    public class UploadPostRequest
    {
        [Required]
        public List<IFormFile> Images { get; set; } = new List<IFormFile>();

        public string? Title { get; set; }

        public string? Caption { get; set; }
    }
}