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
        private ImageSource? _icon;
        private int _processId;
        private string _processName = string.Empty;
        private bool _isActive;
        private bool _isGroup;
        private string _groupName = string.Empty;
        private ObservableCollection<WindowItem> _children = new();

        public IntPtr HWnd
        {
            get => _hWnd;
            set { _hWnd = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

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
