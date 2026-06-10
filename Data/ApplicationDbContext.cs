using JavaIdeMini.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace JavaIdeMini.Data
{
    /// <summary>
    /// Class DbContext của Entity Framework Core đóng vai trò là cầu nối giữa các thực thể C# và Database PostgreSQL.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **DbContext vs EntityManager / JPA**: 
    ///   - Trong Spring Boot JPA, `EntityManager` hoặc các interface `JpaRepository` quản lý việc truy vấn và lưu dữ liệu.
    ///   - Trong .NET, `DbContext` đại diện cho cả Unit of Work và Repository pattern. Nó quản lý kết nối, tracking trạng thái thực thể và thực hiện các transaction.
    /// - **DbSet vs Repository**: 
    ///   - `DbSet<T>` tương đương với một bảng trong DB và cung cấp các phương thức CRUD tương tự như `JpaRepository<T, ID>` trong Spring.
    /// - **OnModelCreating vs JPA mapping**:
    ///   - Phương thức `OnModelCreating` tương đương với cấu hình XML mapping hoặc Fluent mapping trong Hibernate. Đây là nơi định nghĩa các ràng buộc phức tạp (như composite index, unique keys).
    /// </remarks>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// Khởi tạo DbContext với các tùy chọn cấu hình (Connection String, Providers).
        /// </summary>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Tập hợp các bản ghi Profile người dùng.
        /// </summary>
        public DbSet<UsersProfile> UsersProfiles { get; set; }

        /// <summary>
        /// Tập hợp các bản ghi Snippets chứa code Java.
        /// </summary>
        public DbSet<Snippet> Snippets { get; set; }

        /// <summary>
        /// Tập hợp lịch sử biên dịch và chạy mã nguồn.
        /// </summary>
        public DbSet<RunHistory> RunHistories { get; set; }

        /// <summary>
        /// Tập hợp thông tin theo dõi quota chạy code hàng ngày của người dùng.
        /// </summary>
        public DbSet<RunQuota> RunQuotas { get; set; }

        /// <summary>
        /// Cấu hình mối quan hệ và các ràng buộc dữ liệu nâng cao (Fluent API).
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình ràng buộc UNIQUE phức hợp (Composite Unique Constraint) trên bảng run_quota.
            // Mỗi người dùng (user_id) chỉ có duy nhất 1 bản ghi quota cho 1 ngày (date) cụ thể.
            modelBuilder.Entity<RunQuota>()
                .HasIndex(rq => new { rq.UserId, rq.Date })
                .IsUnique()
                .HasDatabaseName("unique_user_date");

            // Thiết lập giá trị mặc định cho ngày của run_quota
            modelBuilder.Entity<RunQuota>()
                .Property(rq => rq.Date)
                .HasConversion(
                    d => d.ToDateTime(TimeOnly.MinValue),
                    d => DateOnly.FromDateTime(d)
                );
        }
    }
}
