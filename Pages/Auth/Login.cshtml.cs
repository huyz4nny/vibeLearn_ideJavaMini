using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using JavaIdeMini.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Pages.Auth
{
    /// <summary>
    /// PageModel xử lý chức năng đăng nhập tài khoản bằng Email/Password hoặc chuyển hướng sang Google OAuth.
    /// </summary>
    public class LoginModel : PageModel
    {
        private readonly SupabaseAuthService _authService;
        private readonly ILogger<LoginModel> _logger;
        private readonly string _supabaseUrl;

        /// <summary>
        /// Khởi tạo LoginModel với các service cần thiết.
        /// </summary>
        public LoginModel(SupabaseAuthService authService, IConfiguration configuration, ILogger<LoginModel> logger)
        {
            _authService = authService;
            _logger = logger;
            _supabaseUrl = configuration["Supabase:Url"] ?? throw new ArgumentNullException("Supabase:Url is missing");
        }

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập Email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        /// <summary>
        /// Đường dẫn động dùng để chuyển hướng người dùng sang Google OAuth.
        /// </summary>
        public string GoogleAuthUrl { get; set; } = string.Empty;

        /// <summary>
        /// Xử lý Get request hiển thị Form đăng nhập.
        /// </summary>
        public IActionResult OnGet(string? msg, string? successMsg)
        {
            // Nếu người dùng đã đăng nhập rồi, tự động chuyển về trang chủ
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Index");
            }

            ErrorMessage = msg;
            SuccessMessage = successMsg;

            // Xây dựng Redirect URL động cho Google OAuth dựa vào host hiện tại (Local / Railway)
            var currentHost = $"{Request.Scheme}://{Request.Host}";
            GoogleAuthUrl = $"{_supabaseUrl}/auth/v1/authorize?provider=google&redirect_to={Uri.EscapeDataString(currentHost + "/auth/callback")}";

            return Page();
        }

        /// <summary>
        /// Xử lý Form Post khi người dùng bấm nút Đăng nhập Email.
        /// </summary>
        public async Task<IActionResult> OnPostAsync()
        {
            // Thiết lập lại GoogleAuthUrl phòng trường hợp form validation failed và phải re-render trang
            var currentHost = $"{Request.Scheme}://{Request.Host}";
            GoogleAuthUrl = $"{_supabaseUrl}/auth/v1/authorize?provider=google&redirect_to={Uri.EscapeDataString(currentHost + "/auth/callback")}";

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await _authService.SignInWithEmailAsync(Email, Password);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage;
                return Page();
            }

            // Lưu JWT Access Token vào Cookie an toàn (HttpOnly & Secure)
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(2) // Thời gian sống mặc định của Supabase JWT thường là 1h-2h
            };

            Response.Cookies.Append("supabase_token", result.AccessToken!, cookieOptions);

            _logger.LogInformation($"User {Email} đã đăng nhập thành công bằng email.");
            return RedirectToPage("/Index");
        }
    }
}
