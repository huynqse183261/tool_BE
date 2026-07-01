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
    public interface IPostService
    {
        Task<ApiResponse<UploadPostResponse>> UploadPostAsync(UploadPostRequest request, int userId);
        Task<ApiResponse<PostDetailResponse>> GetPostDetailAsync(int postId, int userId);
        Task<ApiResponse<bool>> UpdateDraftAsync(int postId, int userId, UpdatePostRequest request);

        // Draft management
        Task<ApiResponse<List<DraftSummaryResponse>>> GetDraftsAsync(int userId);
        Task<ApiResponse<bool>> DeleteDraftAsync(int postId, int userId);

        // Scheduling
        Task<ApiResponse<bool>> SchedulePostAsync(int postId, int userId, SchedulePostRequest request);
        Task<ApiResponse<bool>> CancelScheduleAsync(int postId, int userId);
        Task<ApiResponse<List<PostSummaryResponse>>> GetPublishedPostsAsync(int userId);
    }
}