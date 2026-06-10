using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace JavaIdeMini.Data.Entities
{
    /// <summary>
    /// Thực thể đại diện cho bảng 'users_profile' trong database.
    /// Lưu trữ thông tin bổ sung của người dùng (tên hiển thị, avatar) đồng bộ từ Supabase Auth.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **Property Auto-Properties**: C# hỗ trợ { get; set; } giúp viết gọn code. Trong Java, bạn phải dùng 
    ///   boilerplate code getters/setters hoặc thư viện Lombok (@Data, @Getter, @Setter).
    /// - **Data Annotations**: [Table("users_profile", Schema = "public")] tương đương với @Table(name = "users_profile", schema = "public") trong JPA.
    /// - **UUID/Guid**: C# có kiểu Guid tương đương trực tiếp với java.util.UUID.
    /// - **DateTimeOffset**: Tương đương với OffsetDateTime trong Java, dùng cho các trường timestamptz (Timestamp with timezone).
    /// </remarks>
    [Table("users_profile", Schema = "public")]
    public class UsersProfile
    {
        /// <summary>
        /// Khóa chính (Primary Key) của bảng, đồng thời là Khóa ngoại (Foreign Key) 
        /// trỏ tới bảng 'auth.users' trong schema xác thực nội bộ của Supabase.
        /// </summary>
        /// <remarks>
        /// 💡 **So sánh Java ↔ C#**:
        /// - Annotation [Key] tương đương @Id trong JPA.
        /// - [DatabaseGenerated(DatabaseGeneratedOption.None)] báo cho EF Core rằng giá trị này 
        ///   do ứng dụng/trigger cấp phát (UUID từ auth), không tự tăng tự động.
        /// </remarks>
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid Id { get; set; }

        /// <summary>
        /// Tên đầy đủ của người dùng (ví dụ: lấy từ Google Profile sau khi đăng nhập qua OAuth).
        /// </summary>
        [Column("full_name")]
        public string? FullName { get; set; }

        /// <summary>
        /// Đường dẫn ảnh đại diện (avatar_url) được đồng bộ từ Google OAuth hoặc nhà cung cấp dịch vụ bên thứ ba.
        /// </summary>
        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// Thời điểm cập nhật hồ sơ người dùng lần cuối cùng.
        /// </summary>
        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
