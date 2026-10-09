using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TaskDock11.Services
{
    public static class BrowserProfileService
    {
        private static readonly Dictionary<string, string> _profileMap = new(StringComparer.OrdinalIgnoreCase);
        private static DateTime _lastLoadedTime = DateTime.MinValue;

        /// <summary>
        /// 加载或刷新 Chrome 与 Edge 浏览器的本地配置文件自定义名称
        /// </summary>
        public static void EnsureLoaded()
        {
            if ((DateTime.Now - _lastLoadedTime).TotalSeconds < 10 && _profileMap.Count > 0)
            {
                return;
            }

            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] localStatePaths = new[]
                {
                    Path.Combine(localAppData, @"Google\Chrome\User Data\Local State"),
                    Path.Combine(localAppData, @"Microsoft\Edge\User Data\Local State"),
                    Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Local State")
                };

                foreach (var path in localStatePaths)
                {
                    if (!File.Exists(path)) continue;

                    try
                    {
                        using var stream = File.OpenRead(path);
                        using var doc = JsonDocument.Parse(stream);
                        if (doc.RootElement.TryGetProperty("profile", out var profElem) &&
                            profElem.TryGetProperty("info_cache", out var infoCache))
                        {
                            foreach (var item in infoCache.EnumerateObject())
                            {
                                string dirName = item.Name; // 例如: "Profile 12", "Default"
                                if (item.Value.TryGetProperty("name", out var nameProp))
                                {
                                    string customName = nameProp.GetString() ?? "";
                                    if (!string.IsNullOrWhiteSpace(customName))
                                    {
                                        _profileMap[dirName] = customName;
                                        _profileMap[$"Chrome_{dirName}"] = customName;
                                        _profileMap[$"Edge_{dirName}"] = customName;
                                        _profileMap[$"Google Chrome_{dirName}"] = customName;
                                        
                                        // 针对没有空格的变形也支持 (如 Chrome_Profile12)
                                        string noSpaceDir = dirName.Replace(" ", "");
                                        _profileMap[noSpaceDir] = customName;
                                        _profileMap[$"Chrome_{noSpaceDir}"] = customName;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 忽略读取错误 (如浏览器排他性锁定时)
                    }
                }

                _lastLoadedTime = DateTime.Now;
            }
            catch
            {
            }
        }

        /// <summary>
        /// 将浏览器标题中的用户配置占位符（如 Chrome_Profile 12）智能替换为用户在浏览器设置中自定义的个性化名称
        /// </summary>
        public static string ResolveCustomProfileTitle(string title, string processName)
        {
            if (string.IsNullOrWhiteSpace(title)) return title;

            EnsureLoaded();

            // 1. 完全匹配
            if (_profileMap.TryGetValue(title.Trim(), out var exactName))
            {
                return exactName;
            }

            // 2. 正则匹配 Profile 序号 (例如: Chrome_Profile 12、Profile 12、Edge_Profile 12)
            var match = Regex.Match(title, @"(?:Chrome_|Edge_)?(Profile\s*\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string profileKey = match.Groups[1].Value.Trim();
                if (_profileMap.TryGetValue(profileKey, out var resolvedName))
                {
                    // 替换匹配部分
                    return title.Replace(match.Value, resolvedName);
                }
            }

            return title;
        }
    }
}
