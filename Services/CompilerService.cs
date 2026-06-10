using System;
using System.Collections.Generic;
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
    /// Service kết nối với Wandbox API để biên dịch và chạy code Java.
    /// Hỗ trợ lập trình hướng đối tượng (OOP) đa tệp tin và nhiều packages.
    /// </summary>
    public class CompilerService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<CompilerService> _logger;
        private const string WandboxUrl = "https://wandbox.org/api/compile.json";
        private const string JavaCompiler = "openjdk-jdk-21+35"; // OpenJDK 21 (tương thích ngược hoàn toàn với Java 17)

        /// <summary>
        /// Khởi tạo CompilerService.
        /// </summary>
        public CompilerService(IHttpClientFactory httpClientFactory, ILogger<CompilerService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Gửi danh sách các file Java lên Wandbox API để biên dịch và thực thi.
        /// Hỗ trợ cấu trúc thư mục package phức tạp để ứng dụng OOP.
        /// </summary>
        public async Task<CompileResult> ExecuteJavaCodeAsync(List<JavaFile> files)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var client = _httpClientFactory.CreateClient();

                // 💡 GIẢI PHÁP BIÊN DỊCH ĐA FILE TRÊN WANDBOX:
                // - File code chính trong tham số "code" của Wandbox luôn được ghi vào file 'prog.java'.
                // - Để hỗ trợ người dùng viết class 'public class Main' trong tệp 'Main.java' (và các file OOP khác),
                //   chúng ta truyền một file khởi chạy trung gian 'prog.java' làm code chính để gọi hàm main của lớp Main:
                //   "class prog { public static void main(String[] args) { Main.main(args); } }"
                // - Toàn bộ các file Java của người dùng (kể cả Main.java và các class ở package khác) 
                //   sẽ được truyền qua mảng "codes" với đường dẫn chính xác (ví dụ: "com/example/Dog.java").
                var bootstrapCode = "class prog { public static void main(String[] args) { Main.main(args); } }";

                var wandboxFiles = new List<WandboxFile>();
                foreach (var file in files)
                {
                    wandboxFiles.Add(new WandboxFile
                    {
                        File = file.Path,
                        Code = file.Content
                    });
                }

                var payload = new WandboxRequest
                {
                    Compiler = JavaCompiler,
                    Code = bootstrapCode,
                    Codes = wandboxFiles.ToArray(),
                    CompilerOptionRaw = "--release\n17"
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(WandboxUrl, content);
                stopwatch.Stop();

                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Wandbox API trả về lỗi: {responseString}");
                    return new CompileResult
                    {
                        Success = false,
                        Output = $"Lỗi hệ thống biên dịch Wandbox (HTTP {response.StatusCode}):\n{responseString}",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                var wandboxResponse = JsonSerializer.Deserialize<WandboxResponse>(responseString);

                if (wandboxResponse == null)
                {
                    return new CompileResult
                    {
                        Success = false,
                        Output = "Không nhận được phản hồi kết quả chạy code.",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                bool isSuccess = wandboxResponse.Status == 0;
                
                string output = string.Empty;
                if (!string.IsNullOrEmpty(wandboxResponse.CompilerError))
                {
                    output += "[LỖI BIÊN DỊCH]\n" + wandboxResponse.CompilerError + "\n";
                }
                if (!string.IsNullOrEmpty(wandboxResponse.ProgramError))
                {
                    output += "[LỖI CHƯƠNG TRÌNH (RUNTIME)]\n" + wandboxResponse.ProgramError + "\n";
                }
                if (!string.IsNullOrEmpty(wandboxResponse.ProgramOutput))
                {
                    output += wandboxResponse.ProgramOutput;
                }

                if (string.IsNullOrEmpty(output))
                {
                    output = isSuccess ? "(Chương trình chạy thành công, không có output)" : "(Chương trình lỗi, không có output)";
                }

                return new CompileResult
                {
                    Success = isSuccess,
                    Output = output,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Lỗi xảy ra khi gọi Wandbox API");
                return new CompileResult
                {
                    Success = false,
                    Output = $"Lỗi kết nối tới server biên dịch Wandbox: {ex.Message}",
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
        }
    }

    /// <summary>
    /// Kết quả biên dịch và chạy code Java.
    /// </summary>
    public class CompileResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public int DurationMs { get; set; }
    }

    /// <summary>
    /// Thực thể đại diện cho một file Java trong project.
    /// </summary>
    public class JavaFile
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    internal class WandboxRequest
    {
        [JsonPropertyName("compiler")]
        public string Compiler { get; set; } = "openjdk-jdk-21+35";

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("codes")]
        public WandboxFile[] Codes { get; set; } = Array.Empty<WandboxFile>();

        [JsonPropertyName("compiler-option-raw")]
        public string CompilerOptionRaw { get; set; } = string.Empty;
    }

    internal class WandboxFile
    {
        [JsonPropertyName("file")]
        public string File { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;
    }

    internal class WandboxResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("compiler_output")]
        public string? CompilerOutput { get; set; }

        [JsonPropertyName("compiler_error")]
        public string? CompilerError { get; set; }

        [JsonPropertyName("program_output")]
        public string? ProgramOutput { get; set; }

        [JsonPropertyName("program_error")]
        public string? ProgramError { get; set; }
    }
}
