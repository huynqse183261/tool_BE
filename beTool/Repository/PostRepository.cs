using BookfetSystem.Repositories.Basic;
using Microsoft.EntityFrameworkCore;
using Repositories.DBContext;
using Repositories.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories
{
    public class PostRepository : GenericRepository<Post>
    {
        public PostRepository(toolContext context) : base(context) { }

        // Lấy post kèm images theo postId
        public async Task<Post?> GetPostWithImagesAsync(int postId)
        {
            return await _context.Posts
                .Include(p => p.PostImages)
                .FirstOrDefaultAsync(p => p.Id == postId);
        }

        // Lấy tất cả drafts của user, kèm ảnh đầu tiên để hiển thị thumbnail
        public async Task<List<Post>> GetDraftsByUserAsync(int userId)
        {
            return await _context.Posts
                .Include(p => p.PostImages.OrderBy(i => i.DisplayOrder).Take(1))
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.UpdatedAt)
                .ToListAsync();
        }

        // Lấy tất cả posts theo status, dùng cho scheduling job
        public async Task<List<Post>> GetByStatusAsync(string status)
        {
            return await _context.Posts
                .Include(p => p.PostImages)
                .Include(p => p.PostPlatforms)
                .Where(p => p.Status == status)
                .ToListAsync();
        }

        // Lấy post kèm đầy đủ relations cho publishing job
        public async Task<Post?> GetPostForPublishingAsync(int postId)
        {
            return await _context.Posts
                .Include(p => p.PostImages.OrderBy(i => i.DisplayOrder))
                .Include(p => p.PostPlatforms)
                    .ThenInclude(pp => pp.SocialAccount)
                .FirstOrDefaultAsync(p => p.Id == postId);
        }
        public async Task<List<Post>> GetPublishedByUserAsync(int userId)
        {
            return await _context.Posts
                .Include(p => p.PostImages.OrderBy(i => i.DisplayOrder).Take(1))
                .Include(p => p.PostPlatforms)
                .Where(p => p.UserId == userId && p.Status == "Published")
                .OrderByDescending(p => p.PublishedAt)
                .ToListAsync();
        }
    }
}