using System;
using System.Threading.Tasks;
using JavaIdeMini.Data;
using JavaIdeMini.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Services
{
    /// <summary>
    /// Service quản lý quota (giới hạn lượt chạy code) của người dùng hàng ngày.
    /// Giới hạn: 200 lượt/ngày.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **EF Core Async Operations**: Các hàm DB của EF Core hỗ trợ async (ví dụ: `FirstOrDefaultAsync`, `SaveChangesAsync`) 
    ///   giúp giải phóng luồng xử lý (Thread Pool) trong khi đợi DB phản hồi. 
    ///   Tương tự như Spring Data R2DBC (Reactive) hoặc dùng ExecutorService trong Java.
    /// - **DateOnly**: Được giới thiệu từ .NET 6 để lưu trữ dữ liệu chỉ có Ngày (Year-Month-Day), 
    ///   tránh việc lưu cả Giờ không cần thiết (DateTime). Tương đương `LocalDate` trong Java.
    /// </remarks>
    public class QuotaService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<QuotaService> _logger;
        public const int MaxDailyQuota = 200;

        /// <summary>
        /// Khởi tạo QuotaService.
        /// </summary>
        public QuotaService(ApplicationDbContext dbContext, ILogger<QuotaService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Lấy số lượt chạy còn lại trong ngày hôm nay của người dùng.
        /// </summary>
        public async Task<int> GetRemainingQuotaAsync(Guid userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var quota = await _dbContext.RunQuotas
                .FirstOrDefaultAsync(q => q.UserId == userId && q.Date == today);

            if (quota == null)
            {
                return MaxDailyQuota;
            }

            int remaining = MaxDailyQuota - quota.Count;
            return remaining < 0 ? 0 : remaining;
        }

        /// <summary>
        /// Kiểm tra xem người dùng có còn quota chạy code hay không.
        /// Nếu còn, tự động tăng số lần đã chạy lên 1 (UPSERT).
        /// </summary>
        /// <returns>True nếu còn quota và đã cập nhật thành công, False nếu đã hết quota.</returns>
        public async Task<bool> CheckAndIncrementQuotaAsync(Guid userId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            try
            {
                // Tìm bản ghi quota của user cho ngày hôm nay
                var quota = await _dbContext.RunQuotas
                    .FirstOrDefaultAsync(q => q.UserId == userId && q.Date == today);

                if (quota == null)
                {
                    // Tạo mới bản ghi quota cho ngày hôm nay
                    quota = new RunQuota
                    {
                        UserId = userId,
                        Date = today,
                        Count = 1
                    };
                    _dbContext.RunQuotas.Add(quota);
                }
                else
                {
                    // Nếu đã hết quota, chặn lại
                    if (quota.Count >= MaxDailyQuota)
                    {
                        _logger.LogWarning($"User {userId} đã vượt quá hạn mức {MaxDailyQuota} lần/ngày.");
                        return false;
                    }

                    // Tăng số lần chạy
                    quota.Count++;
                    _dbContext.Entry(quota).State = EntityState.Modified;
                }

                // Lưu thay đổi vào DB (tương đương entityManager.flush() trong Hibernate)
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi xảy ra khi cập nhật quota cho User {userId}");
                // Trong trường hợp DB lỗi, để đảm bảo UX không bị đứt gãy, chúng ta có thể cho phép chạy tiếp,
                // hoặc chặn lại tùy theo chính sách bảo mật. Ở đây chúng ta cho phép chạy để tránh lỗi hệ thống làm gián đoạn người dùng.
                return true;
            }
        }
    }
}
