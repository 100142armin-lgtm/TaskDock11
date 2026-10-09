using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using TaskDock11.Models;
using TaskDock11.Services.Win32;

namespace TaskDock11.Services
{
    public class WindowManagerService : IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private IntPtr _hEventHookForeground = IntPtr.Zero;
        private IntPtr _hEventHookCreateDestroy = IntPtr.Zero;
        private IntPtr _selfHwnd = IntPtr.Zero;
        private NativeMethods.WinEventDelegate? _winEventDelegate;
        private DispatcherTimer? _pollTimer;

        public event Action? WindowsChanged;
        public event Action<IntPtr>? ActiveWindowChanged;

        public WindowManagerService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _winEventDelegate = new NativeMethods.WinEventDelegate(WinEventProc);

            // 监听窗口前台切换
            _hEventHookForeground = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventDelegate,
                0, 0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            // 监听窗口创建/销毁与最小化
            _hEventHookCreateDestroy = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_OBJECT_CREATE,
                NativeMethods.EVENT_OBJECT_DESTROY,
                IntPtr.Zero,
                _winEventDelegate,
                0, 0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            // 兜底定时刷新（每 1.5 秒轻量增量校验一次，避免特殊应用遗漏）
            _pollTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _pollTimer.Tick += (s, e) => WindowsChanged?.Invoke();
            _pollTimer.Start();
        }

        public void SetSelfWindow(IntPtr selfHwnd)
        {
            _selfHwnd = selfHwnd;
        }

        private IntPtr _lastForegroundHwnd = IntPtr.Zero;

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hWnd == IntPtr.Zero || hWnd == _selfHwnd) return;

            _dispatcher.BeginInvoke(() =>
            {
                if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND)
                {
                    _lastForegroundHwnd = hWnd;
                    ActiveWindowChanged?.Invoke(hWnd);
                }
                else
                {
                    WindowsChanged?.Invoke();
                }
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// 工业级 Alt+Tab 规则枚举当前屏幕所有有效打开的应用窗口
        /// </summary>
        public List<WindowItem> GetActiveWindows()
        {
            var result = new List<WindowItem>();
            IntPtr currentForeground = NativeMethods.GetForegroundWindow();

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == _selfHwnd) return true;
                if (!IsAltTabWindow(hWnd)) return true;

                // 提取标题 (支持浏览器等复杂动态标题并平滑回退)
                var sb = new StringBuilder(512);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString().Trim();

                // 获取进程信息
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                int processId = (int)pid;
                string processName = "";
                try
                {
                    using var proc = Process.GetProcessById(processId);
                    processName = proc.ProcessName;
                }
                catch { }

                if (string.IsNullOrEmpty(title))
                {
                    if (!string.IsNullOrEmpty(processName))
                    {
                        title = processName;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    // 智能提炼浏览器标题：剥除 - Google Chrome / - Microsoft Edge 等冗余尾巴，只保留网页核心标题！
                    title = CleanBrowserTitle(title, processName);
                }

                // 提取图标
                var icon = IconHelper.GetWindowIcon(hWnd, processId);

                var item = new WindowItem
                {
                    HWnd = hWnd,
                    Title = title,
                    Icon = icon,
                    ProcessId = processId,
                    ProcessName = processName,
                    IsActive = (hWnd == currentForeground)
                };

                // 人性化通讯软件未读消息角标与在线会议状态检测
                DetectNotificationAndMeeting(item);

                result.Add(item);
                return true;
            }, IntPtr.Zero);

            return result;
        }

        private static string CleanBrowserTitle(string title, string processName)
        {
            if (string.IsNullOrWhiteSpace(title)) return title;
            string pLower = processName.ToLowerInvariant();
            if (pLower.Contains("chrome") || pLower.Contains("edge") || pLower.Contains("brave") || pLower.Contains("firefox") || pLower.Contains("opera"))
            {
                string[] browserSuffixes = new[] 
                { 
                    " - Google Chrome", 
                    " - Microsoft​ Edge", 
                    " - Microsoft Edge", 
                    " - 个人 - Microsoft​ Edge",
                    " - 个人 - Microsoft Edge",
                    " - Mozilla Firefox", 
                    " - Brave" 
                };
                foreach (var suffix in browserSuffixes)
                {
                    if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return title.Substring(0, title.Length - suffix.Length).Trim();
                    }
                }
            }
            return title;
        }

        private static void DetectNotificationAndMeeting(WindowItem item)
        {
            string pLower = item.ProcessName.ToLowerInvariant();
            string title = item.Title;

            // 1. 通讯软件 (微信、企业微信、钉钉、飞书、QQ、Teams、Slack 等)
            bool isChatApp = pLower.Contains("wechat") || pLower.Contains("wxwork") || pLower.Contains("dingtalk") ||
                             pLower.Contains("qq") || pLower.Contains("teams") || pLower.Contains("slack") ||
                             pLower.Contains("feishu") || pLower.Contains("lark") || pLower.Contains("telegram");

            if (isChatApp)
            {
                var match = System.Text.RegularExpressions.Regex.Match(title, @"[\(\[](?<cnt>\d+\+?)[\)\]]");
                if (match.Success)
                {
                    item.HasNotification = true;
                    item.NotificationText = match.Groups["cnt"].Value;
                }
                else if (title.Contains("新消息") || title.Contains("未读"))
                {
                    item.HasNotification = true;
                    item.NotificationText = "!";
                }
            }

            // 2. 在线会议软件 (腾讯会议、Zoom、Teams 等)
            bool isMeetingApp = pLower.Contains("wemeet") || pLower.Contains("zoom") || pLower.Contains("meeting") || pLower.Contains("voov");
            if (isMeetingApp || pLower.Contains("teams"))
            {
                if (title.Contains("会议") || title.Contains("Meeting") || title.Contains("正在共享") || title.Contains("通话"))
                {
                    item.IsInMeeting = true;
                }
            }
        }

        /// <summary>
        /// Windows Alt+Tab 严格过滤算法
        /// </summary>
        private bool IsAltTabWindow(IntPtr hWnd)
        {
            if (!NativeMethods.IsWindowVisible(hWnd))
                return false;

            // 过滤类名（桌面与系统任务栏外壳）
            var classSb = new StringBuilder(256);
            NativeMethods.GetClassName(hWnd, classSb, classSb.Capacity);
            string className = classSb.ToString();
            if (className == "Progman" || className == "WorkerW" ||
                className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd" ||
                className == "Windows.UI.Core.CoreWindow" || className.Contains("Tooltip", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // 根所有者检查
            IntPtr root = NativeMethods.GetAncestor(hWnd, NativeMethods.GA_ROOTOWNER);
            IntPtr lastActive = NativeMethods.GetLastActivePopup(root);
            if (lastActive != hWnd && lastActive != IntPtr.Zero)
            {
                // 如果不是最后激活的顶层弹窗，跳过
            }

            // 检查 ExStyle (过滤 ToolWindow)
            long exStyle = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE).ToInt64();
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 && (exStyle & NativeMethods.WS_EX_APPWINDOW) == 0)
            {
                return false;
            }

            // 检查 Win10/Win11 Cloaked 状态 (虚拟桌面或隐蔽后台UWP窗口)
            try
            {
                if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0)
                {
                    if (cloaked != 0) return false;
                }
            }
            catch { }

            return true;
        }

        /// <summary>
        /// 工业级激活或最小化窗口（单击还原显示，再单击最小化收回）
        /// </summary>
        public void ToggleWindow(WindowItem item)
        {
            if (item == null || item.HWnd == IntPtr.Zero) return;
            IntPtr hWnd = item.HWnd;

            IntPtr fg = NativeMethods.GetForegroundWindow();
            
            // 判定当前窗口是否正处于最前台活动状态：
            bool isAlreadyActive = (fg == hWnd) || item.IsActive || (_lastForegroundHwnd == hWnd);

            if (isAlreadyActive && !NativeMethods.IsIconic(hWnd))
            {
                // 已经在最前台 -> 缩小收起回去 (最小化)
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWMINIMIZED);
                item.IsActive = false;
                _lastForegroundHwnd = IntPtr.Zero;
                return;
            }

            // 还原并放大显示出来
            if (NativeMethods.IsIconic(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }
            else
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWNORMAL);
            }

            // 破解 Windows Foreground Lock 并置顶
            uint fgThread = NativeMethods.GetWindowThreadProcessId(fg, out _);
            uint curThread = NativeMethods.GetCurrentThreadId();
            uint targetThread = NativeMethods.GetWindowThreadProcessId(hWnd, out _);

            if (fgThread != 0 && fgThread != curThread)
            {
                NativeMethods.AttachThreadInput(curThread, fgThread, true);
            }
            if (targetThread != 0 && targetThread != curThread)
            {
                NativeMethods.AttachThreadInput(curThread, targetThread, true);
            }

            NativeMethods.keybd_event(0x12, 0, 0, 0);
            NativeMethods.keybd_event(0x12, 0, 2, 0);

            NativeMethods.SetWindowPos(hWnd, NativeMethods.HWND_TOP, 0, 0, 0, 0, NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.BringWindowToTop(hWnd);
            NativeMethods.SwitchToThisWindow(hWnd, true);

            if (fgThread != 0 && fgThread != curThread)
            {
                NativeMethods.AttachThreadInput(curThread, fgThread, false);
            }
            if (targetThread != 0 && targetThread != curThread)
            {
                NativeMethods.AttachThreadInput(curThread, targetThread, false);
            }

            item.IsActive = true;
            _lastForegroundHwnd = hWnd;
        }

        public void ToggleWindow(IntPtr hWnd)
        {
            var dummyItem = new WindowItem { HWnd = hWnd, IsActive = (_lastForegroundHwnd == hWnd) };
            ToggleWindow(dummyItem);
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        public void CloseWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            NativeMethods.PostMessage(hWnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// 强制结束进程（防卡死霸屏）
        /// </summary>
        public void KillProcess(int processId)
        {
            if (processId <= 0) return;
            try
            {
                using var proc = Process.GetProcessById(processId);
                proc.Kill();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"结束进程失败: {ex.Message}", "TaskDock 11", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void Dispose()
        {
            _pollTimer?.Stop();
            if (_hEventHookForeground != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hEventHookForeground);
                _hEventHookForeground = IntPtr.Zero;
            }
            if (_hEventHookCreateDestroy != IntPtr.Zero)
            {
                NativeMethods.UnhookWinEvent(_hEventHookCreateDestroy);
                _hEventHookCreateDestroy = IntPtr.Zero;
            }
        }
    }
}
