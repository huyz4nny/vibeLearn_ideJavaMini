using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using JavaIdeMini.Data;
using JavaIdeMini.Data.Entities;
using JavaIdeMini.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Pages
{
    /// <summary>
    /// PageModel cho trang chủ IDE. Quản lý việc load danh sách snippet, lịch sử chạy, quota, 
    /// và thực hiện các thao tác CRUD snippet (Lưu, Cập nhật, Xóa).
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **LINQ (Language Integrated Query)**:
    ///   - C# tích hợp LINQ (`Where`, `OrderByDescending`, `Take`, `Select`) giúp truy vấn dữ liệu từ DB trực quan, ngắn gọn. 
    ///   - Trong Java, bạn phải viết JPQL/HQL trong `@Query` hoặc dùng Criteria API phức tạp, hoặc dùng Java Stream API sau khi đã load dữ liệu lên memory.
    /// - **System.Security.Cryptography.RandomNumberGenerator**:
    ///   - Dùng để sinh `share_id` bảo mật ở mức mã hóa. Tương đương `java.security.SecureRandom` trong Java. 
    ///   - Tránh dùng `System.Random` (tương đương `java.util.Random` trong Java) vì nó sử dụng thuật toán giả ngẫu nhiên dựa vào clock, dễ bị đoán trước.
    /// </remarks>
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly QuotaService _quotaService;
        private readonly ILogger<IndexModel> _logger;

        /// <summary>
        /// Khởi tạo IndexModel.
        /// </summary>
        public IndexModel(ApplicationDbContext dbContext, QuotaService quotaService, ILogger<IndexModel> logger)
        {
            _dbContext = dbContext;
            _quotaService = quotaService;
            _logger = logger;
        }

        // Danh sách snippets cá nhân của user hiện tại
        public List<Snippet> UserSnippets { get; set; } = new();

        // Lịch sử 20 lần chạy gần nhất
        public List<RunHistory> RecentHistory { get; set; } = new();

        // Quota còn lại trong ngày
        public int RemainingQuota { get; set; } = QuotaService.MaxDailyQuota;

        // Snippet đang được chỉnh sửa (nếu có)
        public Snippet? CurrentSnippet { get; set; }

        // Code mặc định hiển thị trong Monaco Editor khi mới vào app (định dạng JSON chứa nhiều file và package)
        public string DefaultCode { get; set; } = 
"""
[
  {
    "path": "Main.java",
    "content": "public class Main {\n    public static void main(String[] args) {\n        System.out.println(\"Xin chào C# .NET từ thế giới Java! (Java 17 Compliance)\");\n        \n        // Sử dụng các lớp Dog và Cat từ package animals để ứng dụng OOP\n        animals.Dog myDog = new animals.Dog(\"Cậu Vàng\", 3);\n        animals.Cat myCat = new animals.Cat(\"Mimi\", 2);\n        \n        System.out.println(myDog.getDetail());\n        myDog.makeSound();\n        \n        System.out.println(myCat.getDetail());\n        myCat.makeSound();\n    }\n}"
  },
  {
    "path": "animals/Animal.java",
    "content": "package animals;\n\npublic abstract class Animal {\n    protected String name;\n    protected int age;\n\n    public Animal(String name, int age) {\n        this.name = name;\n        this.age = age;\n    }\n\n    public abstract void makeSound();\n\n    public String getDetail() {\n        return name + \" (\" + age + \" tuổi)\";\n    }\n}"
  },
  {
    "path": "animals/Dog.java",
    "content": "package animals;\n\npublic class Dog extends Animal {\n    public Dog(String name, int age) {\n        super(name, age);\n    }\n\n    @Override\n    public void makeSound() {\n        System.out.println(\"Gâu gâu! \");\n    }\n}"
  },
  {
    "path": "animals/Cat.java",
    "content": "package animals;\n\npublic class Cat extends Animal {\n    public Cat(String name, int age) {\n        super(name, age);\n    }\n\n    @Override\n    public void makeSound() {\n        System.out.println(\"Meo meo! \");\n    }\n}"
  }
]
""";

        /// <summary>
        /// Xử lý load trang chủ. Nếu có tham số id, thực hiện load snippet tương ứng.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(long? id)
        {
            var isAuthed = User.Identity?.IsAuthenticated == true;
            var userIdObj = HttpContext.Items["UserId"];

            if (isAuthed && userIdObj is Guid userId)
            {
                // 1. Lấy danh sách snippet cá nhân của user
                UserSnippets = await _dbContext.Snippets
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.UpdatedAt)
                    .ToListAsync();

                // 2. Lấy 20 lần chạy code gần nhất
                RecentHistory = await _dbContext.RunHistories
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(20)
                    .ToListAsync();

                // 3. Lấy quota còn lại trong ngày
                RemainingQuota = await _quotaService.GetRemainingQuotaAsync(userId);

                // 4. Nếu truyền id, load snippet lên editor
                if (id.HasValue)
                {
                    CurrentSnippet = await _dbContext.Snippets
                        .FirstOrDefaultAsync(s => s.Id == id.Value && s.UserId == userId);
                    
                    if (CurrentSnippet == null)
                    {
                        // Không tìm thấy hoặc không thuộc quyền sở hữu của user
                        return RedirectToPage("/Index");
                    }

                    // Tự động chuyển đổi và chuẩn hóa sang JSON nếu snippet cũ lưu dạng text thuần
                    if (!IsValidJson(CurrentSnippet.Code))
                    {
                        var legacyFiles = new List<JavaFile>
                        {
                            new JavaFile { Path = CurrentSnippet.Title ?? "Main.java", Content = CurrentSnippet.Code }
                        };
                        CurrentSnippet.Code = System.Text.Json.JsonSerializer.Serialize(legacyFiles);
                    }
                }
            }

            return Page();
        }

        /// <summary>
        /// Xử lý Ajax/Form POST để Lưu hoặc Cập nhật Snippet.
        /// </summary>
        public async Task<IActionResult> OnPostSaveSnippetAsync([FromForm] long? id, [FromForm] string title, [FromForm] string code, [FromForm] bool isPublic)
        {
            if (User.Identity?.IsAuthenticated != true || HttpContext.Items["UserId"] is not Guid userId)
            {
                return Challenge(); // Trả về yêu cầu đăng nhập
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(code))
            {
                TempData["ErrorMessage"] = "Tiêu đề và nội dung code không được trống!";
                return RedirectToPage("/Index", new { id });
            }

            try
            {
                Snippet snippet;
                if (id.HasValue && id.Value > 0)
                {
                    // CẬP NHẬT SNIPPET CŨ
                    snippet = await _dbContext.Snippets
                        .FirstOrDefaultAsync(s => s.Id == id.Value && s.UserId == userId);

                    if (snippet == null)
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy snippet để cập nhật.";
                        return RedirectToPage("/Index");
                    }

                    snippet.Title = title;
                    snippet.Code = code;
                    snippet.IsPublic = isPublic;
                    snippet.UpdatedAt = DateTimeOffset.UtcNow;

                    _dbContext.Entry(snippet).State = EntityState.Modified;
                    TempData["SuccessMessage"] = "Đã cập nhật snippet thành công!";
                }
                else
                {
                    // TẠO SNIPPET MỚI
                    snippet = new Snippet
                    {
                        UserId = userId,
                        Title = title,
                        Code = code,
                        IsPublic = isPublic,
                        ShareId = GenerateShortId(), // Sinh mã chia sẻ ngẫu nhiên 8 ký tự
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };

                    _dbContext.Snippets.Add(snippet);
                    TempData["SuccessMessage"] = "Đã lưu snippet mới thành công!";
                }

                await _dbContext.SaveChangesAsync();
                return RedirectToPage("/Index", new { id = snippet.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lưu snippet");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi lưu snippet: " + ex.Message;
                return RedirectToPage("/Index", new { id });
            }
        }

        /// <summary>
        /// Xử lý Form POST để xóa Snippet.
        /// </summary>
        public async Task<IActionResult> OnPostDeleteSnippetAsync([FromForm] long id)
        {
            if (User.Identity?.IsAuthenticated != true || HttpContext.Items["UserId"] is not Guid userId)
            {
                return Challenge();
            }

            try
            {
                var snippet = await _dbContext.Snippets
                    .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

                if (snippet != null)
                {
                    _dbContext.Snippets.Remove(snippet);
                    await _dbContext.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Đã xóa snippet thành công!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Không tìm thấy snippet để xóa hoặc bạn không có quyền.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa snippet");
                TempData["ErrorMessage"] = "Lỗi khi xóa snippet: " + ex.Message;
            }

            return RedirectToPage("/Index");
        }

        /// <summary>
        /// Sinh mã ngắn gồm 8 ký tự ngẫu nhiên bằng RandomNumberGenerator của .NET.
        /// Bảo mật cao cấp (Cryptographically Secure Random), tương đương SecureRandom của Java.
        /// </summary>
        private string GenerateShortId()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var result = new char[8];
            for (int i = 0; i < 8; i++)
            {
                // RandomNumberGenerator.GetInt32 đảm bảo tính ngẫu nhiên an toàn hơn Random.Next()
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
