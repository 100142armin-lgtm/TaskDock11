using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using TaskDock11.Views;

namespace TaskDock11.Services
{
    public class UpdateCheckService
    {
        private static readonly HttpClient _httpClient = new();

        public async Task CheckForUpdatesAsync(string owner, string repo, string currentVersion, bool showPromptIfLatest = false)
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            {
                return;
            }

            try
            {
                string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.Add(new ProductInfoHeaderValue("TaskDock11-App", "1.0"));

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    if (showPromptIfLatest)
                    {
                        MessageBox.Show($"检测更新未响应 (状态码: {response.StatusCode})", "更新检测", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string latestTag = root.GetProperty("tag_name").GetString() ?? "";
                string releaseUrl = root.GetProperty("html_url").GetString() ?? "";
                string releaseNotes = root.TryGetProperty("body", out var bodyProp) ? (bodyProp.GetString() ?? "") : "";

                // 寻找打包的 exe 下载链接
                string? directExeDownloadUrl = null;
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            directExeDownloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(latestTag) && !string.Equals(latestTag.Trim('v', 'V'), currentVersion.Trim('v', 'V'), StringComparison.OrdinalIgnoreCase))
                {
                    var result = MessageBox.Show(
                        $"发现新版本: {latestTag}（当前版本: {currentVersion}）！\n\n更新说明:\n{releaseNotes}\n\n是否立即一键自动下载并无感升级？",
                        "TaskDock 11 在线升级提示",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        string? currentExePath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(directExeDownloadUrl) && !string.IsNullOrEmpty(currentExePath))
                        {
                            // 启动应用内静默下载与平滑自替换 (无模态不抢占焦点，右下角轻量提示)
                            var progressWin = new UpdateProgressWindow(directExeDownloadUrl, currentExePath);
                            progressWin.Show();
                        }
                        else if (!string.IsNullOrEmpty(releaseUrl))
                        {
                            // 兜底打开网页
                            Process.Start(new ProcessStartInfo { FileName = releaseUrl, UseShellExecute = true });
                        }
                    }
                }
                else if (showPromptIfLatest)
                {
                    MessageBox.Show($"当前已是最新版本 ({currentVersion})，无需更新！", "TaskDock 11", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (showPromptIfLatest)
                {
                    MessageBox.Show($"连接云端更新服务失败: {ex.Message}", "更新检测", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
