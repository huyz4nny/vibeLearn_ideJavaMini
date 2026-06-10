using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JavaIdeMini.Data.Entities
{
    /// <summary>
    /// Thực thể đại diện cho bảng 'snippets' trong database.
    /// Lưu trữ các file source code Java do người dùng soạn thảo và lưu lại.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **Generated Value**: [DatabaseGenerated(DatabaseGeneratedOption.Identity)] tương đương với `@GeneratedValue(strategy = GenerationType.IDENTITY)` trong JPA.
    /// - **Foreign Key Mapping**: Thuộc tính điều hướng (Navigation Property) `public virtual UsersProfile? User` kết hợp với `[ForeignKey("UserId")]` 
    ///   giúp EF Core map quan hệ tương tự như `@ManyToOne` và `@JoinColumn(name = "user_id")` trong JPA.
    /// - **Virtual modifier**: Từ khóa `virtual` cho phép EF Core bật cơ chế Lazy Loading cho thuộc tính liên kết.
    /// </remarks>
    [Table("snippets", Schema = "public")]
    public class Snippet
    {
        /// <summary>
        /// Khóa chính tự sinh dạng tự tăng (identity) 64-bit.
        /// </summary>
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// ID của người dùng sở hữu snippet này. Khóa ngoại trỏ đến auth.users.
        /// </summary>
        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>
        /// Tiêu đề của snippet (tên file Java, ví dụ: Main.java).
        /// </summary>
        [Required]
        [Column("title")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Nội dung mã nguồn Java do người dùng viết.
        /// </summary>
        [Required]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Mã định danh chia sẻ duy nhất gồm 8 ký tự ngẫu nhiên.
        /// Cho phép người khác truy cập trực tiếp qua URL s/{share_id} mà không cần đăng nhập.
        /// </summary>
        [Column("share_id")]
        [MaxLength(8)]
        public string? ShareId { get; set; }

        /// <summary>
        /// Trạng thái công khai của snippet. Nếu true, cho phép xem công khai.
        /// </summary>
        [Required]
        [Column("is_public")]
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// Thời điểm snippet được tạo ra.
        /// </summary>
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Thời điểm snippet được cập nhật lần cuối.
        /// </summary>
        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Thuộc tính điều hướng liên kết tới thông tin profile người sở hữu.
        /// </summary>
        [ForeignKey("UserId")]
        public virtual UsersProfile? UserProfile { get; set; }
    }
}
