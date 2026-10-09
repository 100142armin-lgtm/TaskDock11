using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TaskDock11.Models;
using TaskDock11.Services;

namespace TaskDock11.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly WindowManagerService _windowService;
        private readonly SettingsService _settingsService;
        private readonly UpdateCheckService _updateService;

        private DockSettings _settings;
        private WindowItem? _expandedGroup;
        private bool _isGroupPopupOpen;

        public ObservableCollection<WindowItem> Windows { get; } = new();

        public DockSettings Settings
        {
            get => _settings;
            set { _settings = value; OnPropertyChanged(); }
        }

        public WindowItem? ExpandedGroup
        {
            get => _expandedGroup;
            set { _expandedGroup = value; OnPropertyChanged(); }
        }

        public bool IsGroupPopupOpen
        {
            get => _isGroupPopupOpen;
            set { _isGroupPopupOpen = value; OnPropertyChanged(); }
        }

        private readonly HashSet<IntPtr> _hiddenHwnds = new();
        private readonly Dictionary<IntPtr, string> _customNames = new();

        public int HiddenCount => _hiddenHwnds.Count;

        public void HideWindow(WindowItem item)
        {
            if (item.IsGroup)
            {
                foreach (var child in item.Children)
                {
                    _hiddenHwnds.Add(child.HWnd);
                }
            }
            else
            {
                _hiddenHwnds.Add(item.HWnd);
            }

            Windows.Remove(item);
            OnPropertyChanged(nameof(HiddenCount));
        }

        public void RestoreAllHiddenWindows()
        {
            _hiddenHwnds.Clear();
            OnPropertyChanged(nameof(HiddenCount));
            RefreshWindows();
        }

        public void SetCustomName(WindowItem item, string customName)
        {
            if (string.IsNullOrWhiteSpace(customName)) return;
            if (item.IsGroup)
            {
                item.GroupName = customName;
                item.Title = customName;
            }
            else
            {
                _customNames[item.HWnd] = customName;
                item.CustomTitle = customName;
            }
        }

        public MainViewModel(Dispatcher dispatcher)
        {
            _settingsService = new SettingsService();
            _settings = _settingsService.LoadSettings();
            _updateService = new UpdateCheckService();

            _windowService = new WindowManagerService(dispatcher);
            _windowService.WindowsChanged += OnWindowsChanged;
            _windowService.ActiveWindowChanged += OnActiveWindowChanged;

            // 初始刷新
            RefreshWindows();

            // 启动时后台静默检查更新
            _ = _updateService.CheckForUpdatesAsync(_settings.GitHubRepoOwner, _settings.GitHubRepoName, _settings.CurrentVersion, false);
        }

        public void SetSelfWindow(IntPtr hWnd)
        {
            _windowService.SetSelfWindow(hWnd);
        }

        public void SaveSettings()
        {
            _settingsService.SaveSettings(_settings);
            OnPropertyChanged(nameof(Settings));
        }

        public void CheckUpdateManual()
        {
            _ = _updateService.CheckForUpdatesAsync(_settings.GitHubRepoOwner, _settings.GitHubRepoName, _settings.CurrentVersion, true);
        }

        private void OnWindowsChanged()
        {
            RefreshWindows();
        }

        private void OnActiveWindowChanged(IntPtr activeHwnd)
        {
            foreach (var item in Windows)
            {
                if (item.IsGroup)
                {
                    bool groupHasActive = item.Children.Any(c => c.HWnd == activeHwnd);
                    item.IsActive = groupHasActive;
                    foreach (var child in item.Children)
                    {
                        child.IsActive = (child.HWnd == activeHwnd);
                    }
                }
                else
                {
                    item.IsActive = (item.HWnd == activeHwnd);
                }
            }
        }

        public void RefreshWindows()
        {
            var activeList = _windowService.GetActiveWindows();
            var activeHwndSet = new HashSet<IntPtr>(activeList.Select(w => w.HWnd));

            // 1. 清理已关闭的窗口（保留自定义分组与顺序）
            for (int i = Windows.Count - 1; i >= 0; i--)
            {
                var item = Windows[i];
                if (item.IsGroup)
                {
                    for (int c = item.Children.Count - 1; c >= 0; c--)
                    {
                        if (!activeHwndSet.Contains(item.Children[c].HWnd))
                        {
                            item.Children.RemoveAt(c);
                        }
                    }

                    // 如果分组只剩 1 个应用，自动降级恢复为普通窗口
                    if (item.Children.Count == 1)
                    {
                        var soleChild = item.Children[0];
                        Windows[i] = soleChild;
                    }
                    else if (item.Children.Count == 0)
                    {
                        Windows.RemoveAt(i);
                    }
                }
                else
                {
                    if (!activeHwndSet.Contains(item.HWnd))
                    {
                        Windows.RemoveAt(i);
                    }
                }
            }

            // 2. 更新已有窗口的标题/激活状态/消息通知状态，并找出新增的窗口
            var existingHwnds = new HashSet<IntPtr>();
            foreach (var item in Windows)
            {
                if (item.IsGroup)
                {
                    foreach (var child in item.Children)
                    {
                        existingHwnds.Add(child.HWnd);
                        var found = activeList.FirstOrDefault(w => w.HWnd == child.HWnd);
                        if (found != null)
                        {
                            child.Title = found.Title;
                            child.IsActive = found.IsActive;
                            child.HasNotification = found.HasNotification;
                            child.NotificationText = found.NotificationText;
                            child.IsInMeeting = found.IsInMeeting;
                            if (child.Icon == null) child.Icon = found.Icon;
                        }
                    }
                    item.IsActive = item.Children.Any(c => c.IsActive);
                    item.HasNotification = item.Children.Any(c => c.HasNotification);
                    item.IsInMeeting = item.Children.Any(c => c.IsInMeeting);
                }
                else
                {
                    existingHwnds.Add(item.HWnd);
                    var found = activeList.FirstOrDefault(w => w.HWnd == item.HWnd);
                    if (found != null)
                    {
                        item.Title = found.Title;
                        item.IsActive = found.IsActive;
                        item.HasNotification = found.HasNotification;
                        item.NotificationText = found.NotificationText;
                        item.IsInMeeting = found.IsInMeeting;
                        if (item.Icon == null) item.Icon = found.Icon;
                    }
                }
            }

            // 3. 追加新开启的窗口 (过滤已隐藏的应用)
            foreach (var item in activeList)
            {
                if (_hiddenHwnds.Contains(item.HWnd)) continue;

                if (!existingHwnds.Contains(item.HWnd))
                {
                    if (_customNames.TryGetValue(item.HWnd, out var customName))
                    {
                        item.CustomTitle = customName;
                    }
                    Windows.Add(item);
                }
            }
        }

        public void HandleWindowClick(WindowItem item)
        {
            if (item.IsGroup)
            {
                ExpandedGroup = item;
                IsGroupPopupOpen = true;
            }
            else
            {
                _windowService.ToggleWindow(item);
            }
        }

        public void CloseWindow(WindowItem item)
        {
            if (item.IsGroup)
            {
                foreach (var child in item.Children.ToList())
                {
                    _windowService.CloseWindow(child.HWnd);
                }
            }
            else
            {
                _windowService.CloseWindow(item.HWnd);
            }
        }

        public void KillProcess(WindowItem item)
        {
            if (item.IsGroup)
            {
                foreach (var child in item.Children.ToList())
                {
                    _windowService.KillProcess(child.ProcessId);
                }
            }
            else
            {
                _windowService.KillProcess(item.ProcessId);
            }
        }

        /// <summary>
        /// 拖拽排序与拖拽堆叠成文件夹（模式 C）
        /// </summary>
        public void HandleDropMergeOrReorder(WindowItem sourceItem, WindowItem targetItem, bool isMerge)
        {
            if (sourceItem == targetItem) return;

            int sourceIndex = Windows.IndexOf(sourceItem);
            int targetIndex = Windows.IndexOf(targetItem);

            if (isMerge)
            {
                // 合并成文件夹/组
                if (targetItem.IsGroup)
                {
                    Windows.Remove(sourceItem);
                    targetItem.Children.Add(sourceItem);
                }
                else
                {
                    // 两者创建新的组
                    Windows.Remove(sourceItem);
                    int newTargetIndex = Windows.IndexOf(targetItem);
                    var group = new WindowItem
                    {
                        IsGroup = true,
                        GroupName = $"分组 ({targetItem.ProcessName})",
                        Title = $"分组 ({targetItem.ProcessName})",
                        Icon = targetItem.Icon // 组主图标默认沿用
                    };
                    group.Children.Add(targetItem);
                    group.Children.Add(sourceItem);

                    Windows[newTargetIndex] = group;
                }
            }
            else
            {
                // 仅调整前后位置排序
                if (sourceIndex >= 0 && targetIndex >= 0)
                {
                    Windows.Move(sourceIndex, targetIndex);
                }
            }
        }

        public void Dispose()
        {
            _windowService.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
