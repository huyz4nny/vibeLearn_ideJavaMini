using System;
using System.Text.Json;
using System.Threading.Tasks;
using JavaIdeMini.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Middleware
{
    /// <summary>
    /// Middleware tự viết dùng để kiểm soát tần suất chạy code (Rate Limiting) 
    /// bằng cách kiểm tra và trừ quota trong bảng 'run_quota'.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **Middleware short-circuiting**: 
    ///   - Trong C#, nếu Middleware không gọi `await _next(context)`, luồng request sẽ bị ngắt tại đây (Short-Circuit) 
    ///     và trả response về ngay lập tức. Tương đương việc không gọi `filterChain.doFilter(request, response)` trong Java Filter.
    ///   - Trả về mã lỗi HTTP 429 (Too Many Requests) đúng chuẩn RESTful API.
    /// </remarks>
    public class QuotaRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<QuotaRateLimitingMiddleware> _logger;

        /// <summary>
        /// Khởi tạo QuotaRateLimitingMiddleware.
        /// </summary>
        public QuotaRateLimitingMiddleware(RequestDelegate next, ILogger<QuotaRateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Xử lý request đi qua middleware.
        /// </summary>
        public async Task InvokeAsync(HttpContext context, QuotaService quotaService)
        {
            var path = context.Request.Path.Value?.ToLower();

            // Chỉ áp dụng rate limiting cho API chạy code "/api/run"
            if (path != null && path.Equals("/api/run"))
            {
                // Kiểm tra xem user đã được định danh bởi JWT Middleware trước đó chưa
                if (context.Items["UserId"] is not Guid userId)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = "Yêu cầu đăng nhập để chạy code Java." }));
                    return;
                }

                // Kiểm tra và tăng lượt dùng quota của user
                bool hasQuota = await quotaService.CheckAndIncrementQuotaAsync(userId);

                if (!hasQuota)
                {
                    // Trả về HTTP 429 Too Many Requests kèm thông báo tiếng Việt
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    
                    var errorResponse = new
                    {
                        message = "Bạn đã dùng hết hạn mức 200 lượt chạy code của ngày hôm nay! Hãy quay lại vào ngày mai."
                    };

                    _logger.LogWarning($"User {userId} đã cạn kiệt quota chạy code hàng ngày.");
                    await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                    return;
                }
            }

            // Nếu không phải API chạy code hoặc quota hợp lệ, chuyển tiếp tới handler tiếp theo
            await _next(context);
        }
    }
}
