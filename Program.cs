using System;
using System.Security.Claims;
using System.Text.Json;
using JavaIdeMini.Data;
using JavaIdeMini.Data.Entities;
using JavaIdeMini.Middleware;
using JavaIdeMini.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
// 1. ĐĂNG KÝ CÁC DỊCH VỤ VÀO DI CONTAINER (Dependency Injection)
// =========================================================================
// 💡 So sánh Java ↔ C#:
// - AddRazorPages() tương đương với việc cấu hình Spring MVC View Resolver (Thymeleaf/JSP).
// - builder.Services.AddScoped<T>() tương đương với việc khai báo các Bean với scope @RequestScope hoặc @Component trong Spring Boot.
// - builder.Services.AddHttpClient() đăng ký IHttpClientFactory giúp quản lý vòng đời HttpClient chống cạn kiệt socket.

builder.Services.AddRazorPages();

// Hỗ trợ IHttpClientFactory
builder.Services.AddHttpClient();

// Lấy Connection String linh hoạt cho cả môi trường Local (appsettings) và Production (Railway Env Var)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration["DATABASE_URL"] // Fallback cho biến môi trường mặc định của Postgres trên Railway
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"];

if (string.IsNullOrEmpty(connectionString))
{
    // Nếu deploy mà quên set key, app sẽ báo lỗi rõ ràng thay vì crash âm thầm
    Console.WriteLine("CẢNH BÁO: ConnectionString 'DefaultConnection' trống! Vui lòng kiểm tra biến môi trường.");
}

// Đăng ký Entity Framework Core DbContext sử dụng Npgsql (PostgreSQL provider)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // Tối ưu kết nối: Tự động retry nếu kết nối chập chờn
        npgsqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
    }));

// Đăng ký các dịch vụ nghiệp vụ (Business Services)
builder.Services.AddScoped<SupabaseAuthService>();
builder.Services.AddScoped<CompilerService>();
builder.Services.AddScoped<QuotaService>();

var app = builder.Build();

// =========================================================================
// 2. CẤU HÌNH HTTP REQUEST PIPELINE (MIDDLEWARES)
// =========================================================================
// 💡 So sánh Java ↔ C#:
// - Pipeline này hoạt động y hệt Spring Security Filter Chain. Thứ tự gọi app.Use... là thứ tự request đi qua.
// - static files -> routing -> authentication (JWT) -> rate limit -> MVC/Razor.

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // Enforce HTTPS trên môi trường Production
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Đăng ký custom JWT Middleware để phân tích và gán User Identity từ Cookie vào HttpContext
app.UseMiddleware<SupabaseJwtMiddleware>();

// Đăng ký custom Rate Limiting Middleware chặn request vượt quá quota 200 lần/ngày trước khi chạy code
app.UseMiddleware<QuotaRateLimitingMiddleware>();

app.UseAuthorization();

app.MapRazorPages();

// =========================================================================
// 3. ĐỊNH NGHĨA MINIMAL APIS (APIs gọn nhẹ)
// =========================================================================

/// <summary>
/// API chạy code Java (được bảo vệ bởi QuotaRateLimitingMiddleware).
/// </summary>
app.MapPost("/api/run", async (
    HttpContext context, 
    RunRequest request, 
    CompilerService compilerService, 
    QuotaService quotaService,
    ApplicationDbContext dbContext) =>
{
    // Lấy thông tin user đã được xác thực từ HttpContext.Items
    if (context.Items["UserId"] is not Guid userId)
    {
        return Results.Json(new { message = "Yêu cầu đăng nhập." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    List<JavaFile> filesToCompile = new();
    if (request.Files != null && request.Files.Count > 0)
    {
        filesToCompile = request.Files;
    }
    else if (!string.IsNullOrWhiteSpace(request.Code))
    {
        filesToCompile.Add(new JavaFile { Path = "Main.java", Content = request.Code });
    }

    if (filesToCompile.Count == 0)
    {
        return Results.Json(new { message = "Mã nguồn không được để trống." }, statusCode: StatusCodes.Status400BadRequest);
    }

    // Biên dịch và chạy code Java thông qua Wandbox API
    var result = await compilerService.ExecuteJavaCodeAsync(filesToCompile);

    // Serialize danh sách file để lưu vào lịch sử chạy
    string serializedCode = JsonSerializer.Serialize(filesToCompile);

    // Ghi nhận lịch sử chạy code vào database
    var runHistory = new RunHistory
    {
        UserId = userId,
        SnippetId = request.SnippetId,
        Code = serializedCode,
        Output = result.Output,
        Status = result.Success ? "success" : "error",
        DurationMs = result.DurationMs
    };

    dbContext.RunHistories.Add(runHistory);
    await dbContext.SaveChangesAsync();

    // Lấy số quota còn lại trong ngày của user để cập nhật lên giao diện
    int remainingQuota = await quotaService.GetRemainingQuotaAsync(userId);

    return Results.Ok(new
    {
        success = result.Success,
        output = result.Output,
        durationMs = result.DurationMs,
        remainingQuota = remainingQuota
    });
});

/// <summary>
/// Health Check Endpoint phục vụ monitor của Railway.
/// </summary>
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();

/// <summary>
/// DTO chứa request payload chạy code.
/// </summary>
/// <remarks>
/// 💡 **So sánh Java ↔ C#**:
/// - Dòng code dưới đây sử dụng C# `record` type. Tương đương với `public record RunRequest(String code, List<JavaFile> files, Long snippetId) {}` 
///   được giới thiệu từ Java 14. Giúp định nghĩa nhanh cấu trúc dữ liệu bất biến (immutable data carrier) không có logic.
/// </remarks>
public record RunRequest(string? Code, List<JavaFile>? Files, long? SnippetId);
