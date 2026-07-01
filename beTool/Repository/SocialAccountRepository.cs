using BookfetSystem.Repositories.Basic;
using Microsoft.EntityFrameworkCore;
using Repositories.DBContext;
using Repositories.Entities;

namespace Repositories
{
    public class SocialAccountRepository : GenericRepository<SocialAccount>
    {
        public SocialAccountRepository(toolContext context) : base(context) { }

        // Lấy social account active của user cho 1 platform cụ thể
        public async Task<SocialAccount?> GetActiveByUserAndPlatformAsync(int userId, string platform)
        {
            return await _context.SocialAccounts
                .FirstOrDefaultAsync(sa =>
                    sa.UserId == userId &&
                    sa.Platform == platform &&
                    sa.IsActive == true);
        }

        // Lấy tất cả social accounts active của user
        public async Task<List<SocialAccount>> GetActiveByUserAsync(int userId)
        {
            return await _context.SocialAccounts
                .Where(sa => sa.UserId == userId && sa.IsActive == true)
                .ToListAsync();
        }
    }
}