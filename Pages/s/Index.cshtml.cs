using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using JavaIdeMini.Data;
using JavaIdeMini.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using JavaIdeMini.Services;

namespace JavaIdeMini.Pages.s
{
    /// <summary>
    /// PageModel xử lý việc hiển thị public snippet thông qua shortId (share_id) 
    /// và cho phép Fork snippet đó về tài khoản cá nhân của người dùng.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<IndexModel> _logger;

        /// <summary>
        /// Khởi tạo Share IndexModel.
        /// </summary>
        public IndexModel(ApplicationDbContext dbContext, ILogger<IndexModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        // Snippet công khai tìm được
        public Snippet? SharedSnippet { get; set; }

        /// <summary>
        /// OnGet hiển thị snippet công khai dựa trên shortId. Không yêu cầu đăng nhập.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(string shortId)
        {
            if (string.IsNullOrEmpty(shortId))
            {
                return RedirectToPage("/Index");
            }

            // Tìm snippet có share_id khớp và đã được đặt ở chế độ công khai (is_public = true)
            // Đồng thời load luôn thông tin profile của tác giả (UserProfile)
            SharedSnippet = await _dbContext.Snippets
                .Include(s => s.UserProfile)
                .FirstOrDefaultAsync(s => s.ShareId == shortId && s.IsPublic);

            if (SharedSnippet == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy mã nguồn chia sẻ hoặc mã nguồn đã bị đặt ở chế độ riêng tư.";
                return RedirectToPage("/Index");
            }

            // Tự động chuyển đổi và chuẩn hóa sang JSON nếu snippet cũ lưu dạng text thuần
            if (!IsValidJson(SharedSnippet.Code))
            {
                var legacyFiles = new List<JavaFile>
                {
                    new JavaFile { Path = SharedSnippet.Title ?? "Main.java", Content = SharedSnippet.Code }
                };
                SharedSnippet.Code = System.Text.Json.JsonSerializer.Serialize(legacyFiles);
            }

            return Page();
        }

        /// <summary>
        /// Xử lý Fork (sao chép) snippet công khai về tài khoản của user hiện tại.
        /// Yêu cầu phải đăng nhập.
        /// </summary>
        public async Task<IActionResult> OnPostForkAsync(string shortId)
        {
            var isAuthed = User.Identity?.IsAuthenticated == true;
            var userIdObj = HttpContext.Items["UserId"];

            // 1. Kiểm tra đăng nhập
            if (!isAuthed || userIdObj is not Guid userId)
            {
                return RedirectToPage("/Auth/Login", new { msg = "Vui lòng đăng nhập tài khoản để fork (sao chép) mã nguồn này về workspace của bạn." });
            }

            if (string.IsNullOrEmpty(shortId))
            {
                return RedirectToPage("/Index");
            }

            try
            {
                // 2. Tìm snippet gốc
                var originalSnippet = await _dbContext.Snippets
                    .FirstOrDefaultAsync(s => s.ShareId == shortId && s.IsPublic);

                if (originalSnippet == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy mã nguồn gốc để sao chép.";
                    return RedirectToPage("/Index");
                }

                // Tự động chuyển đổi và chuẩn hóa sang JSON nếu snippet gốc vẫn dạng text
                var codeContent = originalSnippet.Code;
                if (!IsValidJson(codeContent))
                {
                    var legacyFiles = new List<JavaFile>
                    {
                        new JavaFile { Path = originalSnippet.Title ?? "Main.java", Content = codeContent }
                    };
                    codeContent = System.Text.Json.JsonSerializer.Serialize(legacyFiles);
                }

                // 3. Tạo snippet mới bản sao cho user hiện tại
                var forkedSnippet = new Snippet
                {
                    UserId = userId,
                    Title = originalSnippet.Title.EndsWith("(Forked)") ? originalSnippet.Title : $"{originalSnippet.Title} (Forked)",
                    Code = codeContent,
                    IsPublic = false, // Mặc định bản sao chép sẽ riêng tư, user tự đổi sau
                    ShareId = GenerateShortId(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                _dbContext.Snippets.Add(forkedSnippet);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"User {userId} đã fork thành công snippet {originalSnippet.Id} sang {forkedSnippet.Id}");
                TempData["SuccessMessage"] = $"Đã fork thành công tệp '{originalSnippet.Title}' về workspace của bạn!";
                
                // Chuyển hướng người dùng về trang chủ và nạp snippet vừa fork
                return RedirectToPage("/Index", new { id = forkedSnippet.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi thực hiện Fork snippet");
                TempData["ErrorMessage"] = "Lỗi khi sao chép mã nguồn: " + ex.Message;
                return RedirectToPage("/s/Index", new { shortId });
            }
        }

        /// <summary>
        /// Sinh mã ngắn gồm 8 ký tự ngẫu nhiên bằng RandomNumberGenerator của .NET.
        /// </summary>
        private string GenerateShortId()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var result = new char[8];
            for (int i = 0; i < 8; i++)
            {
                int index = RandomNumberGenerator.GetInt32(chars.Length);
                result[i] = chars[index];
            }
            return new string(result);
        }

        /// <summary>
        /// Kiểm tra xem chuỗi có phải JSON hợp lệ hay không.
        /// </summary>
        private bool IsValidJson(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return false;
            str = str.Trim();
            if ((str.StartsWith("{") && str.EndsWith("}")) || (str.StartsWith("[") && str.EndsWith("]")))
            {
                try
                {
                    using (var jsonDoc = System.Text.Json.JsonDocument.Parse(str))
                    {
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }
    }
}
