using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Pages.Auth
{
    /// <summary>
    /// PageModel xử lý Callback sau khi người dùng xác thực thành công qua Google OAuth trên Supabase.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **PageModel**: Trong ASP.NET Core Razor Pages, PageModel đóng vai trò vừa là Controller vừa là ViewModel (MVVM pattern), 
    ///   chứa logic xử lý request (OnGet, OnPost) gắn liền với một trang giao diện cụ thể. 
    ///   Trong Spring Boot MVC, bạn sẽ viết một `@Controller` riêng và trả về tên của template Thymeleaf.
    /// - **CookieOptions**: Cấu hình thuộc tính cho Cookie trong .NET tương tự như `Cookie` class và `HttpServletResponse.addCookie()` trong Java Servlet API.
    ///   Chúng ta dùng `HttpOnly = true` để chống tấn công XSS, `Secure = true` bắt buộc truyền qua HTTPS, và `SameSite = SameSiteMode.Lax` để chống CSRF trong OAuth.
    /// </remarks>
    [IgnoreAntiforgeryToken] // Tắt antiforgery vì endpoint POST này được gọi tự động từ client-side javascript sau khi redirect OAuth
    public class CallbackModel : PageModel
    {
        private readonly ILogger<CallbackModel> _logger;

        /// <summary>
        /// Khởi tạo CallbackModel.
        /// </summary>
        public CallbackModel(ILogger<CallbackModel> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Get request hiển thị trang HTML chứa JS để phân tích hash fragment từ URL.
        /// </summary>
        public void OnGet()
        {
            // Chỉ trả về View để JS chạy ở phía client.
        }

        /// <summary>
        /// Post request nhận token gửi lên từ Javascript, lưu vào HttpOnly Cookie.
        /// </summary>
        public IActionResult OnPost([FromBody] TokenPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.AccessToken))
            {
                _logger.LogWarning("Callback POST nhận được payload không hợp lệ.");
                return new BadRequestObjectResult(new { message = "Dữ liệu xác thực không hợp lệ." });
            }

            try
            {
                // Cấu hình lưu trữ JWT Access Token vào Cookie bảo mật (HttpOnly & Secure)
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,             // Ngăn chặn Javascript client-side truy cập token (Chống XSS)
                    Secure = true,               // Chỉ truyền Cookie qua kênh HTTPS mã hóa (Railway sẽ enforce HTTPS)
                    SameSite = SameSiteMode.Lax, // Bảo vệ chống tấn công CSRF (Cross-Site Request Forgery)
                    Expires = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn > 0 ? payload.ExpiresIn : 3600) // Thời gian hết hạn của Cookie khớp với JWT
                };

                Response.Cookies.Append("supabase_token", payload.AccessToken, cookieOptions);
                
                _logger.LogInformation("Lưu JWT token vào Cookie thành công từ Google OAuth Callback.");
                return new JsonResult(new { success = true, redirectTo = "/" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra khi lưu cookie xác thực");
                return new ObjectResult(new { message = "Lỗi hệ thống khi xử lý phiên đăng nhập." }) { StatusCode = 500 };
            }
        }
    }

    /// <summary>
    /// Class biểu diễn dữ liệu token nhận được từ Client JS.
    /// </summary>
    public class TokenPayload
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
