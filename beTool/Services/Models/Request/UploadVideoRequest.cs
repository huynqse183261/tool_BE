using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Services.Models.Request
{
    public class UploadVideoRequest
    {
        [Required]
        public IFormFile Video { get; set; } = null!;

        public string? Caption { get; set; }
    }
}