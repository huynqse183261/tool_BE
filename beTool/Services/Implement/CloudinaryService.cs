using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Services.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Services.Implement
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;
        private readonly IConfiguration _configuration;

        public CloudinaryService(IConfiguration configuration)
        {
            _configuration = configuration;

            var cloudName = _configuration["Cloudinary:CloudName"];
            var apiKey = _configuration["Cloudinary:ApiKey"];
            var apiSecret = _configuration["Cloudinary:ApiSecret"];

            if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
            {
                throw new Exception("Cloudinary configuration is missing");
            }

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("No file provided");
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            
            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new ArgumentException($"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}");
            }

            // Validate file size (max 10MB)
            const int maxFileSize = 10 * 1024 * 1024; // 10MB
            if (file.Length > maxFileSize)
            {
                throw new ArgumentException("File size exceeds 10MB limit");
            }

            // Upload to Cloudinary
            using (var stream = file.OpenReadStream())
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "beTool/posts", // Organize images in a folder
                    UseFilename = false, // Don't use original filename to avoid conflicts
                    UniqueFilename = true, // Generate unique filename
                    Overwrite = false // Don't overwrite existing files
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    throw new Exception($"Cloudinary upload error: {uploadResult.Error.Message}");
                }

                return uploadResult.SecureUrl.ToString();
            }
        }

        public async Task<List<string>> UploadImagesAsync(List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
            {
                throw new ArgumentException("No files provided");
            }

            var uploadedUrls = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var url = await UploadImageAsync(file);
                    uploadedUrls.Add(url);
                }
                catch (Exception ex)
                {
                    // Log error but continue with other files
                    // In production, you might want to use proper logging
                    Console.WriteLine($"Error uploading {file.FileName}: {ex.Message}");
                }
            }

            return uploadedUrls;
        }
        public async Task<string> UploadVideoAsync(IFormFile videoFile)
        {
            // Upload video to Cloudinary with resource_type = video
            using var stream = videoFile.OpenReadStream();
            var uploadParams = new VideoUploadParams
            {
                File = new FileDescription(videoFile.FileName, stream),
                Folder = "beTool/videos",
                UseFilename = true,
                UniqueFilename = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception($"Cloudinary video upload failed: {uploadResult.Error?.Message}");

            return uploadResult.SecureUrl.ToString();
        }
    }
}