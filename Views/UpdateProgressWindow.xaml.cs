using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace TaskDock11.Views
{
    public partial class UpdateProgressWindow : Window
    {
        private static readonly HttpClient _httpClient = new();
        private readonly string _downloadUrl;
        private readonly string _targetExePath;

        public UpdateProgressWindow(string downloadUrl, string targetExePath)
        {
            InitializeComponent();
            _downloadUrl = downloadUrl;
            _targetExePath = targetExePath;

            // 定位至屏幕右下角托盘区上方，完全不阻碍用户中央主工作区
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 24;

            Loaded += async (s, e) => await StartDownloadAndApplyAsync();
        }

        private async Task StartDownloadAndApplyAsync()
        {
            try
            {
                TxtStatus.Text = "正在从云端拉取更新数据包...";
                string tempNewExe = Path.Combine(Path.GetTempPath(), "TaskDock11_update.exe");

                using (var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long? totalBytes = response.Content.Headers.ContentLength;

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(tempNewExe, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    byte[] buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            double percent = (double)totalRead / totalBytes.Value * 100;
                            PbDownload.Value = percent;
                            TxtPercent.Text = $"{percent:F0}%";
                            TxtBytes.Text = $"{totalRead / 1024.0 / 1024.0:F1} MB / {totalBytes.Value / 1024.0 / 1024.0:F1} MB";
                        }
                        else
                        {
                            TxtBytes.Text = $"{totalRead / 1024.0 / 1024.0:F1} MB";
                        }
                    }
                }

                TxtStatus.Text = "下载完成！准备自动重启更新...";
                await Task.Delay(600);

                // 生成批处理脚本执行自替换
                string batchScript = Path.Combine(Path.GetTempPath(), "taskdock11_updater.bat");
                string batchContent = $@"@echo off
chcp 65001 > nul
timeout /t 1 /nobreak > nul
:wait_loop
tasklist | findstr /i ""TaskDock11.exe"" > nul
if %errorlevel% equ 0 (
    timeout /t 1 /nobreak > nul
    goto wait_loop
)
copy /y ""{tempNewExe}"" ""{_targetExePath}""
start """" ""{_targetExePath}""
del ""{tempNewExe}""
del ""%~f0""
";
                File.WriteAllText(batchScript, batchContent);

                // 启动批处理脚本并退出当前主程序
                var psi = new ProcessStartInfo
                {
                    FileName = batchScript,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新下载失败: {ex.Message}", "更新提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }
    }
}
