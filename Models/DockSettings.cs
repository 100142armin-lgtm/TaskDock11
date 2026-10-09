using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TaskDock11.Models
{
    public enum DockEdge
    {
        Float = 0,
        Bottom = 1,
        Top = 2,
        Left = 3,
        Right = 4
    }

    public enum CoexistMode
    {
        ReserveWorkArea = 0, // 挤占工作区避让
        Topmost = 1,         // 始终置顶
        AutoHide = 2         // 自动隐藏
    }

    public class DockSettings : INotifyPropertyChanged
    {
        private string _backgroundColorHex = "#0F172A";
        private double _backgroundOpacity = 0.85;
        private double _cornerRadius = 16.0;
        private double _iconSize = 40.0;
        private int _maxItemsPerRow = 12;
        private DockEdge _edge = DockEdge.Float;
        private CoexistMode _coexist = CoexistMode.Topmost;
        private double _floatLeft = -1;
        private double _floatTop = -1;
        private bool _autoStartWithWindows = false;
        private string _gitHubRepoOwner = "100142armin-lgtm";
        private string _gitHubRepoName = "TaskDock11";
        private string _currentVersion = "v1.0.2";

        public string BackgroundColorHex
        {
            get => _backgroundColorHex;
            set { _backgroundColorHex = value; OnPropertyChanged(); }
        }

        public double BackgroundOpacity
        {
            get => _backgroundOpacity;
            set { _backgroundOpacity = value; OnPropertyChanged(); }
        }

        public double CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; OnPropertyChanged(); }
        }

        public double IconSize
        {
            get => _iconSize;
            set 
            { 
                _iconSize = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(WrapMaxWidth)); 
            }
        }

        public int MaxItemsPerRow
        {
            get => _maxItemsPerRow;
            set 
            { 
                _maxItemsPerRow = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(WrapMaxWidth)); 
            }
        }

        public double WrapMaxWidth => (MaxItemsPerRow > 0 ? MaxItemsPerRow : 12) * (IconSize + 18) + 4;

        public DockEdge Edge
        {
            get => _edge;
            set { _edge = value; OnPropertyChanged(); }
        }

        public CoexistMode Coexist
        {
            get => _coexist;
            set { _coexist = value; OnPropertyChanged(); }
        }

        public double FloatLeft
        {
            get => _floatLeft;
            set { _floatLeft = value; OnPropertyChanged(); }
        }

        public double FloatTop
        {
            get => _floatTop;
            set { _floatTop = value; OnPropertyChanged(); }
        }

        public bool AutoStartWithWindows
        {
            get => _autoStartWithWindows;
            set { _autoStartWithWindows = value; OnPropertyChanged(); }
        }

        public string GitHubRepoOwner
        {
            get => _gitHubRepoOwner;
            set { _gitHubRepoOwner = value; OnPropertyChanged(); }
        }

        public string GitHubRepoName
        {
            get => _gitHubRepoName;
            set { _gitHubRepoName = value; OnPropertyChanged(); }
        }

        public string CurrentVersion
        {
            get => _currentVersion;
            set { _currentVersion = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }
    }
}
