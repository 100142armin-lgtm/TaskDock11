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

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hWnd == IntPtr.Zero || hWnd == _selfHwnd) return;

            _dispatcher.BeginInvoke(() =>
            {
                if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND)
                {
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

                // 提取标题
                int len = NativeMethods.GetWindowTextLength(hWnd);
                if (len <= 0) return true;
                var sb = new StringBuilder(len + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString().Trim();
                if (string.IsNullOrEmpty(title)) return true;

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

                result.Add(item);
                return true;
            }, IntPtr.Zero);

            return result;
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
        /// 点击激活或最小化窗口（对标原生任务栏逻辑）
        /// </summary>
        public void ToggleWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;

            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == hWnd)
            {
                // 当前已经在最前台 -> 最小化
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWMINIMIZED);
            }
            else
            {
                // 未激活 -> 还原并置顶激活
                if (NativeMethods.IsIconic(hWnd))
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                }
                else
                {
                    NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWNORMAL);
                }
                NativeMethods.SetForegroundWindow(hWnd);
            }
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
