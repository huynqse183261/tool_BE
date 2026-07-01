using Services.Models.Common;
using Services.Models.Request;
using Services.Models.Response;
using System.Threading.Tasks;

namespace Services.Interface
{
    public interface IPublishService
    {
        Task<ApiResponse<PublishPostResponse>> PublishToFacebookAsync(int postId, int userId, PublishPostRequest request);
    }
}
