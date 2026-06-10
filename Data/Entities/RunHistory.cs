using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JavaIdeMini.Data.Entities
{
    /// <summary>
    /// Thực thể đại diện cho bảng 'run_history' trong database.
    /// Ghi nhận lịch sử chạy code Java của người dùng và kết quả từ Piston API.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **Nullable Types**: `long?` đại diện cho Nullable Long trong C#. Tương đương với đối tượng `Long` (thay vì kiểu nguyên thủy `long`) trong Java.
    /// - **Foreign Key Relationship**: SnippetId trỏ tới bảng 'snippets' nhưng có thể null (nếu snippet gốc bị xóa, lịch sử chạy vẫn được giữ lại nhờ ON DELETE SET NULL).
    /// </remarks>
    [Table("run_history", Schema = "public")]
    public class RunHistory
    {
        /// <summary>
        /// Khóa chính tự sinh identity 64-bit.
        /// </summary>
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// ID của người dùng thực hiện chạy code. Khóa ngoại trỏ đến auth.users.
        /// </summary>
        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>
        /// ID của snippet (nếu chạy từ một snippet đã lưu). 
        /// Trường này có thể null nếu người dùng chạy code nháp chưa lưu.
        /// </summary>
        [Column("snippet_id")]
        public long? SnippetId { get; set; }

        /// <summary>
        /// Nội dung mã nguồn Java đã thực thi tại thời điểm đó.
        /// </summary>
        [Required]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Output trả về từ compiler engine (stdout hoặc stderr).
        /// </summary>
        [Column("output")]
        public string? Output { get; set; }

        /// <summary>
        /// Trạng thái chạy thành công hay lỗi ('success' hoặc 'error').
        /// </summary>
        [Required]
        [Column("status")]
        public string Status { get; set; } = "success";

        /// <summary>
        /// Thời gian biên dịch và thực thi tính bằng mili-giây.
        /// </summary>
        [Required]
        [Column("duration_ms")]
        public int DurationMs { get; set; }

        /// <summary>
        /// Thời điểm thực hiện chạy code.
        /// </summary>
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Thuộc tính điều hướng liên kết tới snippet (nếu có).
        /// </summary>
        [ForeignKey("SnippetId")]
        public virtual Snippet? Snippet { get; set; }

        /// <summary>
        /// Thuộc tính điều hướng liên kết tới thông tin profile người chạy code.
        /// </summary>
        [ForeignKey("UserId")]
        public virtual UsersProfile? UserProfile { get; set; }
    }
}
