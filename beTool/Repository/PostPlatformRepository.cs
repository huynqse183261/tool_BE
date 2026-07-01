using BookfetSystem.Repositories.Basic;
using Microsoft.EntityFrameworkCore;
using Repositories.DBContext;
using Repositories.Entities;

namespace Repositories
{
    public class PostPlatformRepository : GenericRepository<PostPlatform>
    {
        public PostPlatformRepository(toolContext context) : base(context) { }

        // Lấy tất cả PostPlatform của 1 post
        public async Task<List<PostPlatform>> GetByPostIdAsync(int postId)
        {
            return await _context.PostPlatforms
                .Include(pp => pp.SocialAccount)
                .Where(pp => pp.PostId == postId)
                .ToListAsync();
        }

        // Xóa các PostPlatform chưa publish khi cancel schedule
        public async Task DeletePendingByPostAsync(int postId)
        {
            var pendingPlatforms = await _context.PostPlatforms
                .Where(pp => pp.PostId == postId && pp.Status == "Pending")
                .ToListAsync();

            _context.PostPlatforms.RemoveRange(pendingPlatforms);
        }
    }
}