using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Services
{
    /// <summary>
    /// Service kết nối với Piston API công khai để biên dịch và chạy code Java.
    /// </summary>
    /// <remarks>
    /// 💡 **So sánh Java ↔ C#**:
    /// - **Stopwatch vs System.currentTimeMillis()**:
    ///   - Trong Java, để đo thời gian chạy của một đoạn code, ta thường ghi nhận `System.currentTimeMillis()` trước và sau khi chạy rồi trừ cho nhau.
    ///   - Trong C#, ta dùng class `Stopwatch` chuyên dụng trong namespace `System.Diagnostics` để đo đạc thời gian có độ chính xác cao.
    /// - **HttpClient Lifecycle**:
    ///   - Tránh việc khởi tạo `new HttpClient()` thủ công vì mỗi thực thể HttpClient khi dispose sẽ giải phóng kết nối chậm, dẫn đến "Socket Exhaustion".
    ///   - Dùng `IHttpClientFactory.CreateClient()` giống như dùng `HttpClientBuilder` trong Apache HttpClient để quản lý pool các kết nối.
    /// </remarks>
    public class CompilerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CompilerService> _logger;
        private const string PistonUrl = "https://emkc.org/api/v2/piston/execute";

        /// <summary>
        /// Khởi tạo CompilerService.
        /// </summary>
        public CompilerService(IHttpClientFactory httpClientFactory, ILogger<CompilerService> _logger)
        {
            this._httpClientFactory = httpClientFactory;
            this._logger = _logger;
        }

        /// <summary>
        /// Gửi code Java lên Piston API để biên dịch và thực thi.
        /// </summary>
        public async Task<CompileResult> ExecuteJavaCodeAsync(string javaCode)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient();

                // Chuẩn bị payload gửi lên Piston API
                var payload = new PistonRequest
                {
                    Language = "java",
                    Version = "15.0.2", // Phiên bản Java mặc định của Piston
                    Files = new[]
                    {
                        new PistonFile
                        {
                            Name = "Main.java",
                            Content = javaCode
                        }
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Gửi HTTP POST request
                var response = await client.PostAsync(PistonUrl, content);
                stopwatch.Stop();

                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Piston API trả về lỗi: {responseString}");
                    return new CompileResult
                    {
                        Success = false,
                        Output = $"Lỗi hệ thống biên dịch (HTTP {response.StatusCode}):\n{responseString}",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                var pistonResponse = JsonSerializer.Deserialize<PistonResponse>(responseString);

                if (pistonResponse?.Run == null)
                {
                    return new CompileResult
                    {
                        Success = false,
                        Output = "Không nhận được phản hồi kết quả chạy code.",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                // Nếu Piston biên dịch/chạy có lỗi (stderr có dữ liệu hoặc exit code khác 0)
                bool isSuccess = pistonResponse.Run.Code == 0 && string.IsNullOrEmpty(pistonResponse.Run.Stderr);
                string output = isSuccess ? pistonResponse.Run.Stdout : pistonResponse.Run.Output;

                return new CompileResult
                {
                    Success = isSuccess,
                    Output = string.IsNullOrEmpty(output) ? "(Không có output)" : output,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Lỗi xảy ra khi gọi Piston API");
                return new CompileResult
                {
                    Success = false,
                    Output = $"Lỗi kết nối tới server biên dịch: {ex.Message}",
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }
    }

    /// <summary>
    /// Kết quả trả về sau khi biên dịch và chạy code.
    /// </summary>
    public class CompileResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public int DurationMs { get; set; }
    }

    // Các class DTO ánh xạ cấu trúc request/response của Piston API
    internal class PistonRequest
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = "java";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "15.0.2";

        [JsonPropertyName("files")]
        public PistonFile[] Files { get; set; } = Array.Empty<PistonFile>();
    }

    internal class PistonFile
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "Main.java";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    internal class PistonResponse
    {
        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("run")]
        public PistonRunResult? Run { get; set; }
    }

    internal class PistonRunResult
    {
        [JsonPropertyName("stdout")]
        public string Stdout { get; set; } = string.Empty;

        [JsonPropertyName("stderr")]
        public string Stderr { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("signal")]
        public string? Signal { get; set; }

        [JsonPropertyName("output")]
        public string Output { get; set; } = string.Empty;
    }
}
