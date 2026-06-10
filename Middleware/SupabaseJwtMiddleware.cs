using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using JavaIdeMini.Services;
using Microsoft.AspNetCore.Http;

namespace JavaIdeMini.Middleware
{
    /// <summary>
    /// Middleware kiểm tra JWT token của Supabase từ Cookie, xác thực với Auth Server 
    /// và thiết lập thông tin định danh (User Claims) vào HttpContext.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **Middleware vs Spring Security Filter Chain**: 
    ///   - Trong Spring Boot / Spring Security, bạn cấu hình một chuỗi các Filters (`OncePerRequestFilter`) 
    ///     để chặn request, giải mã JWT từ Header `Authorization` và set vào `SecurityContextHolder`.
    ///   - Trong ASP.NET Core, ta dùng Middleware Pipeline. Request đi qua các Middleware kế tiếp nhau qua delegate `_next`. 
    ///     Ta gán thông tin user vào `context.User` (kiểu `ClaimsPrincipal`), tương đương với `SecurityContextHolder.getContext().setAuthentication(...)`.
    /// - **Cookie-based JWT vs Session Cookie**:
    ///   - Session Cookie lưu trữ session ID trên server (Stateful), server phải quản lý session store (Redis, DB, Memory).
    ///   - JWT lưu trữ toàn bộ trạng thái trong token tự đóng gói (Stateless). Chúng ta lưu JWT trong Cookie với cờ 
    ///     `HttpOnly` (chống XSS JS đọc trộm) và `Secure` (chỉ truyền qua HTTPS) để đạt được sự bảo mật tối ưu của Session và tính linh hoạt của JWT.
    /// </remarks>
    public class SupabaseJwtMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Khởi tạo middleware với delegate trỏ tới middleware tiếp theo trong pipeline.
        /// </summary>
        public SupabaseJwtMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Điểm xử lý yêu cầu HTTP đi qua middleware.
        /// </summary>
        public async Task InvokeAsync(HttpContext context, SupabaseAuthService authService)
        {
            // Bỏ qua kiểm tra đối với các request static files hoặc health check để tối ưu hiệu năng
            var path = context.Request.Path.Value?.ToLower();
            if (path != null && (
                path.StartsWith("/css") || 
                path.StartsWith("/js") || 
                path.StartsWith("/lib") || 
                path.StartsWith("/favicon.ico") ||
                path.Equals("/health")))
            {
                await _next(context);
                return;
            }

            // Đọc Access Token từ Cookie "supabase_token"
            var token = context.Request.Cookies["supabase_token"];

            if (!string.IsNullOrEmpty(token))
            {
                // Gọi API của Supabase để kiểm tra token có hợp lệ và lấy thông tin user
                var supabaseUser = await authService.GetUserAsync(token);

                if (supabaseUser != null && Guid.TryParse(supabaseUser.Id, out Guid userId))
                {
                    // Lấy thông tin metadata (avatar, full_name) do Google OAuth hoặc SignUp cấp
                    string fullName = string.Empty;
                    string avatarUrl = string.Empty;

                    if (supabaseUser.UserMetadata != null)
                    {
                        if (supabaseUser.UserMetadata.TryGetValue("full_name", out var fnObj) || 
                            supabaseUser.UserMetadata.TryGetValue("name", out fnObj))
                        {
                            fullName = fnObj?.ToString() ?? string.Empty;
                        }

                        if (supabaseUser.UserMetadata.TryGetValue("avatar_url", out var avObj) || 
                            supabaseUser.UserMetadata.TryGetValue("picture", out avObj))
                        {
                            avatarUrl = avObj?.ToString() ?? string.Empty;
                        }
                    }

                    // Nếu không có full_name, lấy phần trước của email làm tên hiển thị
                    if (string.IsNullOrEmpty(fullName))
                    {
                        fullName = supabaseUser.Email.Split('@')[0];
                    }

                    // Thiết lập các thông tin này vào HttpContext.Items để dễ dàng truy cập trong cùng request
                    context.Items["UserId"] = userId;
                    context.Items["UserEmail"] = supabaseUser.Email;
                    context.Items["UserFullName"] = fullName;
                    context.Items["UserAvatarUrl"] = avatarUrl;
                    context.Items["AccessToken"] = token;

                    // Tạo danh sách Claims để tích hợp với hệ thống Identity/Authorization mặc định của ASP.NET Core
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Email, supabaseUser.Email),
                        new Claim(ClaimTypes.Name, fullName),
                        new Claim("AvatarUrl", avatarUrl)
                    };

                    var identity = new ClaimsIdentity(claims, "SupabaseAuth");
                    context.User = new ClaimsPrincipal(identity);
                }
                else
                {
                    // Token hết hạn hoặc không hợp lệ -> Xóa cookie để tránh gửi request rác lần sau
                    context.Response.Cookies.Delete("supabase_token");
                }
            }

            // Chuyển tiếp request đến middleware tiếp theo (Routing, Authorization, Pages, v.v.)
            await _next(context);
        }
    }
}
