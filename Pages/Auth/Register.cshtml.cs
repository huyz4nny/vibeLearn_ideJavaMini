using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using JavaIdeMini.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Pages.Auth
{
    /// <summary>
    /// PageModel xử lý đăng ký tài khoản người dùng mới bằng Email & Password.
    /// </summary>
    public class RegisterModel : PageModel
    {
        private readonly SupabaseAuthService _authService;
        private readonly ILogger<RegisterModel> _logger;

        /// <summary>
        /// Khởi tạo RegisterModel.
        /// </summary>
        public RegisterModel(SupabaseAuthService authService, ILogger<RegisterModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập Email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ {2} ký tự trở lên.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Hiển thị trang đăng ký.
        /// </summary>
        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToPage("/Index");
            }
            return Page();
        }

        /// <summary>
        /// Xử lý submit form đăng ký.
        /// </summary>
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var result = await _authService.SignUpWithEmailAsync(Email, Password);

            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage;
                return Page();
            }

            _logger.LogInformation($"Tài khoản mới được tạo: {Email}");
            
            // Redirect sang trang đăng nhập kèm theo message thành công
            return RedirectToPage("/Auth/Login", new { successMsg = "Đăng ký thành công! Hãy dùng tài khoản vừa tạo để đăng nhập." });
        }
    }
}
