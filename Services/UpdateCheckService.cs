using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace TaskDock11.Services
{
    public class UpdateCheckService
    {
        private static readonly HttpClient _httpClient = new();

        public async Task CheckForUpdatesAsync(string owner, string repo, string currentVersion, bool showPromptIfLatest = false)
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo) || owner == "YourOrgOrUser")
            {
                if (showPromptIfLatest)
                {
                    MessageBox.Show(
                        "请在设置中配置正确的 GitHub 仓库所有者和仓库名称，即可开启团队自动更新检测！",
                        "TaskDock 11 更新提示",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
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
                        MessageBox.Show($"检测更新失败，GitHub 返回状态码: {response.StatusCode}", "更新检测", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string latestTag = root.GetProperty("tag_name").GetString() ?? "";
                string releaseUrl = root.GetProperty("html_url").GetString() ?? "";
                string releaseNotes = root.TryGetProperty("body", out var bodyProp) ? (bodyProp.GetString() ?? "") : "";

                if (!string.IsNullOrEmpty(latestTag) && !string.Equals(latestTag.Trim('v', 'V'), currentVersion.Trim('v', 'V'), StringComparison.OrdinalIgnoreCase))
                {
                    var result = MessageBox.Show(
                        $"发现新版本: {latestTag}（当前版本: {currentVersion}）！\n\n更新内容说明:\n{releaseNotes}\n\n是否立即前往下载最新版？",
                        "TaskDock 11 版本更新提醒",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(releaseUrl))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = releaseUrl,
                            UseShellExecute = true
                        });
                    }
                }
                else if (showPromptIfLatest)
                {
                    MessageBox.Show($"当前已是最新版本 ({currentVersion})！", "TaskDock 11", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (showPromptIfLatest)
                {
                    MessageBox.Show($"网络连接异常，无法连接到 GitHub: {ex.Message}", "更新检测", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
