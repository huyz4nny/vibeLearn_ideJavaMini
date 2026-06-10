using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JavaIdeMini.Data.Entities
{
    /// <summary>
    /// Thực thể đại diện cho bảng 'run_quota' trong database.
    /// Giới hạn số lần chạy code của mỗi user (tối đa 200 lần/ngày).
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **Date Representation**: Trong .NET, `DateOnly` đại diện cho giá trị ngày không kèm giờ (YYYY-MM-DD), 
    ///   tương đương với `java.time.LocalDate` trong Java 8+.
    /// - **Unique Constraint**: Trong C# EF Core, ràng buộc unique phức hợp (Composite Unique Constraint) 
    ///   thường được cấu hình qua Fluent API trong DbContext hơn là Annotations trên Model.
    /// </remarks>
    [Table("run_quota", Schema = "public")]
    public class RunQuota
    {
        /// <summary>
        /// Khóa chính tự sinh identity 64-bit.
        /// </summary>
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// ID của người dùng chịu giới hạn quota. Khóa ngoại trỏ đến auth.users.
        /// </summary>
        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Ngày áp dụng quota (chỉ chứa ngày, không chứa giờ).
        /// </summary>
        [Required]
        [Column("date")]
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        /// <summary>
        /// Số lần chạy code đã thực hiện trong ngày này. Tối đa là 200.
        /// </summary>
        [Required]
        [Column("count")]
        public int Count { get; set; } = 0;

        /// <summary>
        /// Thuộc tính điều hướng liên kết tới thông tin profile.
        /// </summary>
        [ForeignKey("UserId")]
        public virtual UsersProfile? UserProfile { get; set; }
    }
}
