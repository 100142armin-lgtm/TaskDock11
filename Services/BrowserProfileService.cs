using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TaskDock11.Services
{
    public static class BrowserProfileService
    {
        private static readonly Dictionary<string, string> _profileMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<IntPtr, string> _hwndProfileCache = new();
        private static DateTime _lastLoadedTime = DateTime.MinValue;

        #region Win32 Property Store for PKEY_AppUserModel_ID

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [Out, MarshalAs(UnmanagedType.IUnknown)] out object? propertyStore);

        private static readonly Guid IID_IPropertyStore = new("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
        private static readonly Guid PKEY_AppUserModel_ID = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
        private const uint PID_AppUserModel_ID = 5;

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint cProps);
            [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
            [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
            [PreserveSig] int Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PROPVARIANT
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pwszVal;
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PROPVARIANT pvar);

        private static string? GetAppUserModelId(IntPtr hWnd)
        {
            try
            {
                Guid iid = IID_IPropertyStore;
                int hr = SHGetPropertyStoreForWindow(hWnd, ref iid, out object? storeObj);
                if (hr == 0 && storeObj is IPropertyStore store)
                {
                    var key = new PROPERTYKEY { fmtid = PKEY_AppUserModel_ID, pid = PID_AppUserModel_ID };
                    var pv = new PROPVARIANT();
                    try
                    {
                        if (store.GetValue(ref key, out pv) == 0)
                        {
                            if (pv.vt == 31 && pv.pwszVal != IntPtr.Zero) // VT_LPWSTR = 31
                            {
                                return Marshal.PtrToStringUni(pv.pwszVal);
                            }
                        }
                    }
                    finally
                    {
                        PropVariantClear(ref pv);
                    }
                }
            }
            catch {}
            return null;
        }

        #endregion

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
        /// 从窗口句柄精确获取 Chrome / Edge 个人资料名称 (如 "广告-Cheng Hui")
        /// </summary>
        public static string? GetProfileNameForWindow(IntPtr hWnd, string processName)
        {
            if (hWnd == IntPtr.Zero) return null;

            if (_hwndProfileCache.TryGetValue(hWnd, out var cachedName))
            {
                return cachedName;
            }

            EnsureLoaded();

            string? aumid = GetAppUserModelId(hWnd);
            if (!string.IsNullOrWhiteSpace(aumid))
            {
                // Chrome 的 AppUserModelID 格式通常如：
                // Chrome.UserData.Profile 12 或 Chrome.Profile 12 或 Google.Chrome.Profile 12
                // 或 Microsoft.Edge.Profile 12 等
                var match = Regex.Match(aumid, @"(?:Profile\s*\d+|Default)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string profileDir = match.Value;
                    if (_profileMap.TryGetValue(profileDir, out var resolvedName))
                    {
                        _hwndProfileCache[hWnd] = resolvedName;
                        return resolvedName;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 智能将浏览器标题或配置映射为用户自定义的个人资料昵称
        /// </summary>
        public static string ResolveCustomProfileTitle(string title, string processName, IntPtr hWnd = default)
        {
            if (string.IsNullOrWhiteSpace(title)) return title;

            EnsureLoaded();

            // 如果有窗口句柄，先尝试通过 AppUserModelID 查找
            if (hWnd != IntPtr.Zero)
            {
                string? nameFromHwnd = GetProfileNameForWindow(hWnd, processName);
                if (!string.IsNullOrWhiteSpace(nameFromHwnd))
                {
                    return nameFromHwnd;
                }
            }

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
                    return title.Replace(match.Value, resolvedName);
                }
            }

            return title;
        }
    }
}
