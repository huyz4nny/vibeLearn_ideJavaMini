using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Pages.Auth
{
    /// <summary>
    /// PageModel xử lý chức năng đăng xuất tài khoản bằng cách xóa JWT Cookie.
    /// </summary>
    public class LogoutModel : PageModel
    {
        private readonly ILogger<LogoutModel> _logger;

        /// <summary>
        /// Khởi tạo LogoutModel.
        /// </summary>
        public LogoutModel(ILogger<LogoutModel> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Xử lý Get request để đăng xuất tài khoản.
        /// </summary>
        public IActionResult OnGet()
        {
            return ExecuteLogout();
        }

        /// <summary>
        /// Xử lý Post request để đăng xuất tài khoản (khuyên dùng để chống CSRF logout).
        /// </summary>
        public IActionResult OnPost()
        {
            return ExecuteLogout();
        }

        private IActionResult ExecuteLogout()
        {
            if (Request.Cookies.ContainsKey("supabase_token"))
            {
                Response.Cookies.Delete("supabase_token");
                _logger.LogInformation("Đã xóa JWT cookie 'supabase_token' - Đăng xuất thành công.");
            }

            return RedirectToPage("/Index");
        }
    }
}
