using Services.Models.Common;
using Services.Models.Request;
using Services.Models.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interface
{
    public interface IAiGenerationService
    {
        Task<ApiResponse<GenerateCaptionResponse>> GenerateCaptionAsync(int postId, GenerateCaptionRequest request);
        Task<ApiResponse<GenerateCaptionResponse>> RegenerateCaptionAsync(int postId);
    }
}
