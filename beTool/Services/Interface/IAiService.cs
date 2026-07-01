using System.Collections.Generic;
using System.Threading.Tasks;
using Services.Models.Request;

namespace Services.Interface
{
    public interface IAiService
    {
        // Basic image analysis and caption generation
        Task<(string caption, string analysis, string contentType, string tone, string? title, List<string>? hashtags)> 
            GenerateBrandAwareCaptionAsync(List<string> imageUrls, string templateType, string? brandVoice, GenerateCaptionRequest request);
        
        // Regenerate caption using previous analysis (cheaper)
        Task<string> RegenerateCaptionAsync(string previousAnalysis, List<string> imageUrls, string? newPrompt);
    }
}
