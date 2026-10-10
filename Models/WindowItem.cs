using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace TaskDock11.Models
{
    public class WindowItem : INotifyPropertyChanged
    {
        private IntPtr _hWnd;
        private string _title = string.Empty;
        private string _customTitle = string.Empty;
        private string _profileTitle = string.Empty;
        private ImageSource? _icon;
        private int _processId;
        private string _processName = string.Empty;
        private bool _isActive;
        private bool _isGroup;
        private string _groupName = string.Empty;
        private ObservableCollection<WindowItem> _children = new();

        // 智能状态增强 (通讯软件与在线会议)
        private bool _hasNotification;
        private string _notificationText = string.Empty;
        private bool _isInMeeting;

        public IntPtr HWnd
        {
            get => _hWnd;
            set { _hWnd = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set 
            { 
                _title = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(HasSubtitle));
            }
        }

        public string CustomTitle
        {
            get => _customTitle;
            set
            {
                _customTitle = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(HasSubtitle));
            }
        }

        public string ProfileTitle
        {
            get => _profileTitle;
            set
            {
                _profileTitle = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(HasSubtitle));
            }
        }

        /// <summary>
        /// 核心展示名称：优先手动重命名 > 浏览器个人资料专属名字 (如 "广告-Cheng Hui") > 网页/窗口标题
        /// </summary>
        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_customTitle)) return _customTitle;
                if (!string.IsNullOrWhiteSpace(_profileTitle)) return _profileTitle;
                return _title;
            }
        }

        /// <summary>
        /// 当存在个人资料专属名称或手动重命名且与当前网页标题不同时，可在悬浮窗中展示原网页标题作为副标题
        /// </summary>
        public bool HasSubtitle => (!string.IsNullOrWhiteSpace(_customTitle) || !string.IsNullOrWhiteSpace(_profileTitle)) 
                                    && !string.IsNullOrWhiteSpace(_title) 
                                    && !string.Equals(DisplayTitle, _title, StringComparison.OrdinalIgnoreCase);

        public ImageSource? Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public int ProcessId
        {
            get => _processId;
            set { _processId = value; OnPropertyChanged(); }
        }

        public string ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public bool IsGroup
        {
            get => _isGroup;
            set { _isGroup = value; OnPropertyChanged(); }
        }

        public string GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        public bool HasNotification
        {
            get => _hasNotification;
            set { _hasNotification = value; OnPropertyChanged(); }
        }

        public string NotificationText
        {
            get => _notificationText;
            set { _notificationText = value; OnPropertyChanged(); }
        }

        public bool IsInMeeting
        {
            get => _isInMeeting;
            set { _isInMeeting = value; OnPropertyChanged(); }
        }

        public ObservableCollection<WindowItem> Children
        {
            get => _children;
            set { _children = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
