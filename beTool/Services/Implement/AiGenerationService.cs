using Microsoft.Extensions.Configuration;
using Repositories;
using Repositories.Entities;
using Services.Interface;
using Services.Models.Common;
using Services.Models.Request;
using Services.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Services.Implement
{
    public class AiGenerationService : IAiGenerationService
    {
        private readonly PostRepository _postRepository;
        private readonly PostImageRepository _postImageRepository;
        private readonly IAiService _aiService;
        private readonly AiGenerationRepository _aiGenerationRepository;
        private readonly IConfiguration _configuration;

        public AiGenerationService(
            PostRepository postRepository,
            PostImageRepository postImageRepository,
            IAiService aiService,
            AiGenerationRepository aiGenerationRepository,
            IConfiguration configuration)
        {
            _postRepository = postRepository;
            _postImageRepository = postImageRepository;
            _aiService = aiService;
            _aiGenerationRepository = aiGenerationRepository;
            _configuration = configuration;
        }

        public async Task<ApiResponse<GenerateCaptionResponse>> GenerateCaptionAsync(int postId, GenerateCaptionRequest request)
        {
            try
            {
                var contentType = request.EffectiveContentType;


                // Get the post
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null)
                {
                    return new ApiResponse<GenerateCaptionResponse>
                    {
                        Success = false,
                        Message = "Post not found"
                    };
                }

                // Get all images for this post
                var postImages = _postImageRepository.GetAll()
                    .Where(pi => pi.PostId == postId)
                    .OrderBy(pi => pi.DisplayOrder)
                    .ToList();

                if (postImages.Count == 0)
                {
                    return new ApiResponse<GenerateCaptionResponse>
                    {
                        Success = false,
                        Message = "No images found for this post"
                    };
                }

                // Extract image URLs
                var imageUrls = postImages.Select(pi => pi.ImageUrl).ToList();

                // Generate brand-aware caption using AI
                if (string.IsNullOrWhiteSpace(contentType))
                {
                    return new ApiResponse<GenerateCaptionResponse>
                    {
                        Success = false,
                        Message = "ContentType is required (user-selected template type)"
                    };
                }

                var aiResult = await _aiService.GenerateBrandAwareCaptionAsync(imageUrls, contentType, null, request);
                var caption = aiResult.caption;
                var analysis = aiResult.analysis;
                var tone = aiResult.tone;
                var title = aiResult.title;
                var hashtags = aiResult.hashtags;


                // Save AI generation record
                var aiGeneration = new AiGeneration
                {
                    PostId = postId,
                    Prompt = $"TemplateType={contentType}",
                    GeneratedCaption = caption,
                    VisionAnalysis = analysis,
                    Model = "gpt-4o-mini", // OpenAI model
                    TokensUsed = 0,
                    CreatedAt = DateTime.UtcNow
                };

                await _aiGenerationRepository.CreateAsync(aiGeneration);
                await _aiGenerationRepository.SaveAsync();

                // Update post caption
                post.Caption = caption;
                post.ContentType = contentType;
                await _postRepository.UpdateAsync(post);
                await _postRepository.SaveAsync();

                // Prepare response
                var response = new GenerateCaptionResponse
                {
                    PostId = postId,
                    Caption = caption,
                    Analysis = analysis,
                    Model = aiGeneration.Model,
                    ContentType = contentType,
                    Tone = tone,
                    Title = title,
                    Hashtags = hashtags
                };

                return new ApiResponse<GenerateCaptionResponse>
                {
                    Success = true,
                    Message = "Caption generated successfully",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<GenerateCaptionResponse>
                {
                    Success = false,
                    Message = $"AI generation failed: {ex.Message}"
                };
            }
        }

        public async Task<ApiResponse<GenerateCaptionResponse>> RegenerateCaptionAsync(int postId)
        {
            try
            {
                // Get the post
                var post = await _postRepository.GetByIdAsync(postId);
                if (post == null)
                {
                    return new ApiResponse<GenerateCaptionResponse>
                    {
                        Success = false,
                        Message = "Post not found"
                    };
                }

                // Get the latest AI generation for this post
                var latestGeneration = _aiGenerationRepository.GetAll()
                    .Where(ag => ag.PostId == postId)
                    .OrderByDescending(ag => ag.CreatedAt)
                    .FirstOrDefault();

                if (latestGeneration == null || string.IsNullOrEmpty(latestGeneration.VisionAnalysis))
                {
                    return new ApiResponse<GenerateCaptionResponse>
                    {
                        Success = false,
                        Message = "No previous analysis found. Please generate caption first."
                    };
                }

                // Get image URLs (for context, but won't be sent to AI)
                var postImages = _postImageRepository.GetAll()
                    .Where(pi => pi.PostId == postId)
                    .OrderBy(pi => pi.DisplayOrder)
                    .Select(pi => pi.ImageUrl)
                    .ToList();

                // Regenerate caption using previous analysis (cheaper & faster)
                var newCaption = await _aiService.RegenerateCaptionAsync(
                    latestGeneration.VisionAnalysis,
                    postImages,
                    null
                );

                // Save new AI generation record
                var aiGeneration = new AiGeneration
                {
                    PostId = postId,
                    Prompt = "Regenerate caption",
                    GeneratedCaption = newCaption,
                    VisionAnalysis = latestGeneration.VisionAnalysis, // Reuse previous analysis
                    Model = "gpt-4o-mini", // OpenAI model
                    TokensUsed = 0,
                    CreatedAt = DateTime.UtcNow
                };

                await _aiGenerationRepository.CreateAsync(aiGeneration);
                await _aiGenerationRepository.SaveAsync();

                // Update post caption
                post.Caption = newCaption;
                await _postRepository.UpdateAsync(post);
                await _postRepository.SaveAsync();

                // Prepare response
                var response = new GenerateCaptionResponse
                {
                    PostId = postId,
                    Caption = newCaption,
                    Analysis = latestGeneration.VisionAnalysis,
                    Model = aiGeneration.Model
                };

                return new ApiResponse<GenerateCaptionResponse>
                {
                    Success = true,
                    Message = "Caption regenerated successfully",
                    Data = response
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<GenerateCaptionResponse>
                {
                    Success = false,
                    Message = $"AI regeneration failed: {ex.Message}"
                };
            }
        }
    }
}
