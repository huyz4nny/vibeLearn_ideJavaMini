using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace JavaIdeMini.Services
{
    /// <summary>
    /// Service biên dịch và chạy trực tiếp mã nguồn Java trên máy chủ (Self-hosted Compiler).
    /// Hỗ trợ lập trình hướng đối tượng (OOP) đa tệp tin, nhiều packages và giới hạn thời gian (Timeout).
    /// </summary>
    public class CompilerService
    {
        private readonly ILogger<CompilerService> _logger;

        /// <summary>
        /// Khởi tạo CompilerService.
        /// Giữ lại HttpClientFactory để tương thích ngược cấu hình DI cũ trong Program.cs.
        /// </summary>
        public CompilerService(IHttpClientFactory httpClientFactory, ILogger<CompilerService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Biên dịch và chạy danh sách các file Java cục bộ trên server.
        /// </summary>
        public async Task<CompileResult> ExecuteJavaCodeAsync(List<JavaFile> files)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Tạo thư mục chạy tạm thời trong thư mục scratch của workspace
            var scratchDir = Path.Combine(Directory.GetCurrentDirectory(), "scratch");
            if (!Directory.Exists(scratchDir))
            {
                Directory.CreateDirectory(scratchDir);
            }
            
            var runId = Guid.NewGuid().ToString("N");
            var tempDir = Path.Combine(scratchDir, $"run_{runId}");
            var binDir = Path.Combine(tempDir, "bin");

            try
            {
                // 1. Tạo các thư mục cần thiết
                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(binDir);

                // 2. Ghi các file Java của người dùng và tạo thư mục con (package) tương ứng
                var sourceFiles = new List<string>();
                foreach (var file in files)
                {
                    if (string.IsNullOrWhiteSpace(file.Path) || string.IsNullOrWhiteSpace(file.Content))
                    {
                        continue;
                    }

                    // Chuẩn hóa đường dẫn file (thay đổi / thành kí tự ngăn cách thư mục của hệ điều hành)
                    var normalizedPath = file.Path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    var fullFilePath = Path.Combine(tempDir, normalizedPath);
                    
                    // Tạo thư mục cha nếu file nằm trong subfolder (package)
                    var parentDir = Path.GetDirectoryName(fullFilePath);
                    if (parentDir != null && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    // Ghi nội dung file (sử dụng UTF-8 không có BOM để tránh lỗi tương thích của Java compiler)
                    var utf8WithoutBom = new UTF8Encoding(false);
                    await File.WriteAllTextAsync(fullFilePath, file.Content, utf8WithoutBom);
                    
                    // Thêm vào danh sách file biên dịch
                    sourceFiles.Add(normalizedPath);
                }

                if (sourceFiles.Count == 0)
                {
                    return new CompileResult
                    {
                        Success = false,
                        Output = "Không tìm thấy tệp mã nguồn Java hợp lệ để thực thi.",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                // 3. Tạo file sources.txt ghi danh sách các file cần compile (sử dụng UTF-8 không BOM để tránh lỗi của javac)
                var sourcesTxtPath = Path.Combine(tempDir, "sources.txt");
                await File.WriteAllLinesAsync(sourcesTxtPath, sourceFiles, new UTF8Encoding(false));

                // 4. Gọi trình biên dịch javac
                var javacStartInfo = new ProcessStartInfo
                {
                    FileName = "javac",
                    // Chỉ định compile release 17, xuất kết quả ra thư mục bin
                    Arguments = "--release 17 -d bin @sources.txt",
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var javacProcess = Process.Start(javacStartInfo);
                if (javacProcess == null)
                {
                    return new CompileResult
                    {
                        Success = false,
                        Output = "Không thể khởi động trình biên dịch `javac`. Vui lòng kiểm tra xem JDK đã được cài đặt và cấu hình PATH chưa.",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                // Chờ compile hoàn thành (timeout 10s)
                var javacOutputTask = javacProcess.StandardOutput.ReadToEndAsync();
                var javacErrorTask = javacProcess.StandardError.ReadToEndAsync();
                
                if (!javacProcess.WaitForExit(10000))
                {
                    javacProcess.Kill();
                    return new CompileResult
                    {
                        Success = false,
                        Output = "[LỖI BIÊN DỊCH]\nQuá thời gian biên dịch (javac timeout 10 giây).",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                var javacOutput = await javacOutputTask;
                var javacError = await javacErrorTask;

                if (javacProcess.ExitCode != 0)
                {
                    return new CompileResult
                    {
                        Success = false,
                        Output = "[LỖI BIÊN DỊCH]\n" + (!string.IsNullOrEmpty(javacError) ? javacError : javacOutput),
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                // 5. Chạy mã nguồn Java đã biên dịch (gọi java -cp bin Main)
                var javaStartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = "-cp bin Main",
                    WorkingDirectory = tempDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var javaProcess = Process.Start(javaStartInfo);
                if (javaProcess == null)
                {
                    return new CompileResult
                    {
                        Success = false,
                        Output = "Không thể khởi động môi trường thực thi `java`.",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                // Chờ thực thi chương trình (timeout 5s để chặn vòng lặp vô tận)
                var javaOutputTask = javaProcess.StandardOutput.ReadToEndAsync();
                var javaErrorTask = javaProcess.StandardError.ReadToEndAsync();

                if (!javaProcess.WaitForExit(5000))
                {
                    javaProcess.Kill(true); // Kill process tree
                    return new CompileResult
                    {
                        Success = false,
                        Output = "[LỖI CHƯƠNG TRÌNH]\nQuá thời gian thực thi (Bị ngắt sau 5 giây để tránh vòng lặp vô hạn).",
                        DurationMs = (int)stopwatch.ElapsedMilliseconds
                    };
                }

                stopwatch.Stop();
                var javaOutput = await javaOutputTask;
                var javaError = await javaErrorTask;

                var finalOutput = string.Empty;
                if (!string.IsNullOrEmpty(javaError))
                {
                    finalOutput += "[LỖI CHƯƠNG TRÌNH (RUNTIME)]\n" + javaError + "\n";
                }
                if (!string.IsNullOrEmpty(javaOutput))
                {
                    finalOutput += javaOutput;
                }

                if (string.IsNullOrEmpty(finalOutput))
                {
                    finalOutput = javaProcess.ExitCode == 0 
                        ? "(Chương trình chạy thành công, không có output)" 
                        : $"(Chương trình kết thúc với mã lỗi {javaProcess.ExitCode})";
                }

                return new CompileResult
                {
                    Success = javaProcess.ExitCode == 0,
                    Output = finalOutput,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình biên dịch và chạy Java nội bộ.");
                return new CompileResult
                {
                    Success = false,
                    Output = $"Lỗi hệ thống biên dịch nội bộ: {ex.Message}",
                    DurationMs = (int)stopwatch.ElapsedMilliseconds
                };
            }
            finally
            {
                // Dọn dẹp thư mục tạm
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Không thể xóa thư mục tạm: {tempDir}");
                }
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
}
