using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TaskDock11.Models;
using TaskDock11.Services;
using TaskDock11.ViewModels;
using TaskDock11.Views;

namespace TaskDock11
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly AppBarService _appBarService;
        private Point _dragStartPoint;
        private bool _isDraggingItem = false;
        private DispatcherTimer _autoHideTimer;
        private bool _isTemporarilyHidden = false;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainViewModel(Dispatcher);
            DataContext = _viewModel;
            _appBarService = new AppBarService();

            _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _autoHideTimer.Tick += (s, e) =>
            {
                _autoHideTimer.Stop();
                PerformAutoHide(true);
            };

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            _viewModel.SetSelfWindow(helper.Handle);

            ApplySettingsAppearance();
            ApplyDockPosition();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _appBarService.Unregister();
            _viewModel.Dispose();
        }

        /// <summary>
        /// 应用外观设置（磨砂、颜色、透明度）
        /// </summary>
        public void ApplySettingsAppearance()
        {
            var s = _viewModel.Settings;

            // 1. 设置背景色与透明度
            Color rawColor;
            try
            {
                rawColor = (Color)ColorConverter.ConvertFromString(s.BackgroundColorHex);
            }
            catch
            {
                rawColor = Color.FromRgb(15, 23, 42); // 默认高科技黑曜石
            }

            var brushColor = Color.FromArgb((byte)(s.BackgroundOpacity * 255), rawColor.R, rawColor.G, rawColor.B);
            MainShell.Background = new SolidColorBrush(brushColor);

            // 2. 亚克力磨砂毛玻璃 (与用户选择的色泽完美实时混合)
            AcrylicHelper.EnableBlur(this, s.EnableAcrylicBlur, rawColor, s.BackgroundOpacity);

            // 3. 屏幕并存与置顶
            Topmost = (s.Coexist == CoexistMode.Topmost || s.Coexist == CoexistMode.AutoHide);
        }

        /// <summary>
        /// 应用停靠位置与屏幕工作区挤占
        /// </summary>
        public void ApplyDockPosition()
        {
            var s = _viewModel.Settings;
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            // 如果使用挤占工作区避让
            if (s.Coexist == CoexistMode.ReserveWorkArea && s.Edge != DockEdge.Float)
            {
                _appBarService.Register(this);
                _appBarService.SetDockEdge(s.Edge, (int)ActualWidth, (int)ActualHeight);
            }
            else
            {
                _appBarService.Unregister();
            }

            // 摆放位置
            switch (s.Edge)
            {
                case DockEdge.Bottom:
                    Left = (screenW - ActualWidth) / 2;
                    Top = screenH - ActualHeight - 55; // 紧贴或略高于 Win11 默认底栏
                    break;
                case DockEdge.Top:
                    Left = (screenW - ActualWidth) / 2;
                    Top = 10;
                    break;
                case DockEdge.Left:
                    Left = 10;
                    Top = (screenH - ActualHeight) / 2;
                    break;
                case DockEdge.Right:
                    Left = screenW - ActualWidth - 10;
                    Top = (screenH - ActualHeight) / 2;
                    break;
                case DockEdge.Float:
                default:
                    if (s.FloatLeft > 0 && s.FloatTop > 0)
                    {
                        Left = s.FloatLeft;
                        Top = s.FloatTop;
                    }
                    break;
            }
        }

        #region 窗口拖拽与自动隐藏

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_viewModel.Settings.Edge == DockEdge.Float && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
                _viewModel.Settings.FloatLeft = Left;
                _viewModel.Settings.FloatTop = Top;
                _viewModel.SaveSettings();
            }
        }

        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_viewModel.Settings.Coexist == CoexistMode.AutoHide && _isTemporarilyHidden)
            {
                _autoHideTimer.Stop();
                PerformAutoHide(false);
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_viewModel.Settings.Coexist == CoexistMode.AutoHide && _viewModel.Settings.Edge != DockEdge.Float)
            {
                _autoHideTimer.Start();
            }
        }

        private void PerformAutoHide(bool hide)
        {
            _isTemporarilyHidden = hide;
            var edge = _viewModel.Settings.Edge;
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            if (hide)
            {
                switch (edge)
                {
                    case DockEdge.Bottom: Top = screenH - 4; break;
                    case DockEdge.Top: Top = -ActualHeight + 4; break;
                    case DockEdge.Left: Left = -ActualWidth + 4; break;
                    case DockEdge.Right: Left = screenW - 4; break;
                }
                Opacity = 0.3;
            }
            else
            {
                ApplyDockPosition();
                Opacity = 1.0;
            }
        }

        #endregion

        #region 应用图标交互、拖拽排序与拖拽分组 (模式 C)

        private void Item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingItem) return;
            if (sender is FrameworkElement elem && elem.DataContext is WindowItem item)
            {
                _viewModel.HandleWindowClick(item);
            }
        }

        private void Item_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement elem && elem.DataContext is WindowItem item)
            {
                var currentPos = e.GetPosition(null);
                if (Math.Abs(currentPos.X - _dragStartPoint.X) > 10 || Math.Abs(currentPos.Y - _dragStartPoint.Y) > 10)
                {
                    _isDraggingItem = true;
                    DragDrop.DoDragDrop(elem, item, DragDropEffects.Move | DragDropEffects.Link);
                    _isDraggingItem = false;
                }
            }
            else if (e.LeftButton == MouseButtonState.Released)
            {
                _dragStartPoint = e.GetPosition(null);
            }
        }

        private void Item_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void Item_Drop(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is WindowItem targetItem)
            {
                if (e.Data.GetData(typeof(WindowItem)) is WindowItem sourceItem && sourceItem != targetItem)
                {
                    // 判定是否是叠放合并成组：
                    // 如果按住 Ctrl 拖拽，或者直接松在目标正上方，视为合并为分组；否则视为调整前后顺序
                    bool isMerge = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                    if (!isMerge)
                    {
                        // 智能识别：如果在中心重合区域松开，自动触发合并
                        var pos = e.GetPosition(elem);
                        if (pos.X > elem.ActualWidth * 0.25 && pos.X < elem.ActualWidth * 0.75)
                        {
                            isMerge = true;
                        }
                    }

                    _viewModel.HandleDropMergeOrReorder(sourceItem, targetItem, isMerge);
                }
            }
            _isDraggingItem = false;
        }

        private void GroupChild_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is WindowItem child)
            {
                _viewModel.IsGroupPopupOpen = false;
                _viewModel.HandleWindowClick(child);
            }
        }

        #endregion

        #region 右键菜单事件

        private void MenuItemActivate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WindowItem item)
            {
                _viewModel.HandleWindowClick(item);
            }
        }

        private void MenuItemClose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WindowItem item)
            {
                _viewModel.CloseWindow(item);
            }
        }

        private void MenuItemKill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WindowItem item)
            {
                var res = MessageBox.Show($"确定要强制结束进程【{item.ProcessName}】吗？未保存的数据可能会丢失。",
                    "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    _viewModel.KillProcess(item);
                }
            }
        }

        private void MenuItemUngroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is WindowItem item && item.IsGroup)
            {
                int idx = _viewModel.Windows.IndexOf(item);
                if (idx >= 0)
                {
                    _viewModel.Windows.RemoveAt(idx);
                    foreach (var child in item.Children.Reverse())
                    {
                        _viewModel.Windows.Insert(idx, child);
                    }
                }
            }
        }

        private void MenuRefresh_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshWindows();
        }

        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWin = new SettingsWindow(this, _viewModel)
            {
                Owner = this
            };

            if (settingsWin.ShowDialog() == true)
            {
                ApplySettingsAppearance();
                ApplyDockPosition();
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}