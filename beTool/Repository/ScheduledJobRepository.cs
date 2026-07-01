using BookfetSystem.Repositories.Basic;
using Microsoft.EntityFrameworkCore;
using Repositories.DBContext;
using Repositories.Entities;

namespace Repositories
{
    public class ScheduledJobRepository : GenericRepository<ScheduledJob>
    {
        public ScheduledJobRepository(toolContext context) : base(context) { }

        // Lấy các job sẵn sàng chạy — dùng cho Hangfire polling fallback
        public async Task<List<ScheduledJob>> GetPendingJobsDueAsync()
        {
            return await _context.ScheduledJobs
                .Include(j => j.Post)
                .Where(j => j.Status == "Pending" && j.ExecuteAt <= DateTime.UtcNow)
                .OrderBy(j => j.ExecuteAt)
                .ToListAsync();
        }

        // Lấy pending job của 1 post — để cancel
        public async Task<ScheduledJob?> GetPendingByPostAsync(int postId)
        {
            return await _context.ScheduledJobs
                .FirstOrDefaultAsync(j => j.PostId == postId && j.Status == "Pending");
        }

        // Xóa pending job khi cancel schedule
        public async Task DeletePendingByPostAsync(int postId)
        {
            var pendingJob = await _context.ScheduledJobs
                .Where(j => j.PostId == postId && j.Status == "Pending")
                .ToListAsync();

            _context.ScheduledJobs.RemoveRange(pendingJob);
        }
    }
}