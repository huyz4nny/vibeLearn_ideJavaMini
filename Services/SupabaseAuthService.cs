using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Services
{
    /// <summary>
    /// Service xử lý xác thực người dùng bằng cách gọi trực tiếp REST API của Supabase Auth.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **IHttpClientFactory**: Trong .NET, ta dùng `IHttpClientFactory` để khởi tạo `HttpClient`. 
    ///   Nó tự động quản lý vòng đời của các HttpMessageHandler phía dưới để tránh lỗi "Socket Exhaustion" (cạn kiệt cổng kết nối). 
    ///   Tương đương với việc cấu hình `RestTemplate` pool hoặc dùng `OkHttpClient` với connection pool trong Java.
    /// - **async/await**: Cơ chế bất đồng bộ của C# dựa trên từ khóa `async` và `await` trả về một `Task` hoặc `Task<T>`. 
    ///   Tương đương với `CompletableFuture` hoặc `Mono`/`Flux` (Reactive) trong Java nhưng cú pháp của C# trực quan và dễ đọc hơn (giống code đồng bộ).
    /// - **System.Text.Json**: Thư viện JSON built-in của .NET. Tương đương với Jackson Object Mapper hoặc Gson trong Java.
    /// </remarks>
    public class SupabaseAuthService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _supabaseUrl;
        private readonly string _supabaseAnonKey;
        private readonly ILogger<SupabaseAuthService> _logger;

        /// <summary>
        /// Khởi tạo SupabaseAuthService với HttpClientFactory và cấu hình từ appsettings.
        /// </summary>
        public SupabaseAuthService(
            IHttpClientFactory httpClientFactory, 
            IConfiguration configuration,
            ILogger<SupabaseAuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _supabaseUrl = configuration["Supabase:Url"] ?? throw new ArgumentNullException("Supabase:Url is missing");
            _supabaseAnonKey = configuration["Supabase:AnonKey"] ?? configuration["SUPABASE_ANON_KEY"] ?? throw new ArgumentNullException("Supabase:AnonKey is missing");
            _logger = logger;
        }

        private HttpClient CreateClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_supabaseUrl);
            client.DefaultRequestHeaders.Add("apikey", _supabaseAnonKey);
            return client;
        }

        /// <summary>
        /// Đăng ký tài khoản mới bằng Email và Mật khẩu.
        /// </summary>
        public async Task<AuthResult> SignUpWithEmailAsync(string email, string password)
        {
            try
            {
                var client = CreateClient();
                var payload = JsonSerializer.Serialize(new { email, password });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("/auth/v1/signup", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Đăng ký thất bại: {responseContent}");
                    var errorRes = JsonSerializer.Deserialize<ErrorResponse>(responseContent);
                    return new AuthResult { Success = false, ErrorMessage = errorRes?.Message ?? "Đăng ký không thành công." };
                }

                var authResponse = JsonSerializer.Deserialize<SupabaseAuthResponse>(responseContent);
                return new AuthResult
                {
                    Success = true,
                    AccessToken = authResponse?.AccessToken,
                    RefreshToken = authResponse?.RefreshToken,
                    Email = authResponse?.User?.Email,
                    UserId = authResponse?.User?.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình SignUp");
                return new AuthResult { Success = false, ErrorMessage = "Lỗi kết nối hệ thống xác thực." };
            }
        }

        /// <summary>
        /// Đăng nhập bằng Email và Mật khẩu.
        /// </summary>
        public async Task<AuthResult> SignInWithEmailAsync(string email, string password)
        {
            try
            {
                var client = CreateClient();
                var payload = JsonSerializer.Serialize(new { email, password });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                // API SignIn của Supabase yêu cầu grant_type=password
                var response = await client.PostAsync("/auth/v1/token?grant_type=password", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Đăng nhập thất bại: {responseContent}");
                    var errorRes = JsonSerializer.Deserialize<ErrorResponse>(responseContent);
                    return new AuthResult { Success = false, ErrorMessage = errorRes?.Message ?? "Email hoặc mật khẩu không chính xác." };
                }

                var authResponse = JsonSerializer.Deserialize<SupabaseAuthResponse>(responseContent);
                return new AuthResult
                {
                    Success = true,
                    AccessToken = authResponse?.AccessToken,
                    RefreshToken = authResponse?.RefreshToken,
                    Email = authResponse?.User?.Email,
                    UserId = authResponse?.User?.Id
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình SignIn");
                return new AuthResult { Success = false, ErrorMessage = "Lỗi kết nối hệ thống xác thực." };
            }
        }

        /// <summary>
        /// Lấy thông tin User hiện tại từ JWT token.
        /// Dùng để validate JWT token với Supabase Auth Server.
        /// </summary>
        public async Task<SupabaseUser?> GetUserAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken)) return null;

            try
            {
                var client = CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync("/auth/v1/user");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<SupabaseUser>(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xác thực Access Token với Supabase");
                return null;
            }
        }
    }

    /// <summary>
    /// Class biểu diễn kết quả trả về của quá trình Auth.
    /// </summary>
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? Email { get; set; }
        public string? UserId { get; set; }
    }

    /// <summary>
    /// Phản hồi lỗi từ Supabase API.
    /// </summary>
    public class ErrorResponse
    {
        [JsonPropertyName("msg")]
        public string? Message { get; set; }
        
        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    /// <summary>
    /// Response trả về từ Supabase Token/SignUp API.
    /// </summary>
    public class SupabaseAuthResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("user")]
        public SupabaseUser? User { get; set; }
    }

    /// <summary>
    /// Thông tin User chi tiết từ Supabase.
    /// </summary>
    public class SupabaseUser
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("user_metadata")]
        public Dictionary<string, object>? UserMetadata { get; set; }
    }
}
