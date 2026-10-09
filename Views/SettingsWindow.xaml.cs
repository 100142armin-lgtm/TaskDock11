using System;
using System.Windows;
using System.Windows.Controls;
using TaskDock11.Models;
using TaskDock11.Services;
using TaskDock11.ViewModels;

namespace TaskDock11.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private readonly MainViewModel _viewModel;
        private readonly DockSettings _originalSettings;
        private bool _isInitializing = true;

        public SettingsWindow(MainWindow mainWindow, MainViewModel viewModel)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _viewModel = viewModel;

            // 克隆原始配置（用于取消时完美复原）
            _originalSettings = new DockSettings
            {
                BackgroundColorHex = _viewModel.Settings.BackgroundColorHex,
                BackgroundOpacity = _viewModel.Settings.BackgroundOpacity,
                EnableAcrylicBlur = _viewModel.Settings.EnableAcrylicBlur,
                IconSize = _viewModel.Settings.IconSize,
                CornerRadius = _viewModel.Settings.CornerRadius,
                Edge = _viewModel.Settings.Edge,
                Coexist = _viewModel.Settings.Coexist,
                FloatLeft = _viewModel.Settings.FloatLeft,
                FloatTop = _viewModel.Settings.FloatTop,
                AutoStartWithWindows = _viewModel.Settings.AutoStartWithWindows,
                GitHubRepoOwner = _viewModel.Settings.GitHubRepoOwner,
                GitHubRepoName = _viewModel.Settings.GitHubRepoName,
                CurrentVersion = _viewModel.Settings.CurrentVersion
            };

            // 加载科技感 Logo
            ImgLogo.Source = LogoGenerator.CreateTechLogo();

            LoadCurrentSettings();
            _isInitializing = false;
        }

        private void LoadCurrentSettings()
        {
            var s = _viewModel.Settings;
            ChkAcrylic.IsChecked = s.EnableAcrylicBlur;
            SliderOpacity.Value = s.BackgroundOpacity;
            SliderIconSize.Value = s.IconSize;
            TxtBgColor.Text = s.BackgroundColorHex;
            CmbEdge.SelectedIndex = (int)s.Edge;
            CmbCoexist.SelectedIndex = (int)s.Coexist;
            ChkAutoStart.IsChecked = s.AutoStartWithWindows;
        }

        /// <summary>
        /// 实时联动：任何控件发生变化，主窗口立刻实时渲染
        /// </summary>
        private void SettingControl_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyLivePreview();
        }

        private void TxtBgColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            ApplyLivePreview();
        }

        private void PresetColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                TxtBgColor.Text = hex;
                ApplyLivePreview();
            }
        }

        private void ApplyLivePreview()
        {
            var s = _viewModel.Settings;
            s.EnableAcrylicBlur = ChkAcrylic.IsChecked == true;
            s.BackgroundOpacity = SliderOpacity.Value;
            s.IconSize = SliderIconSize.Value;

            string colorText = TxtBgColor.Text.Trim();
            if (!colorText.StartsWith("#")) colorText = "#" + colorText;
            if (colorText.Length == 7 || colorText.Length == 9)
            {
                s.BackgroundColorHex = colorText;
            }

            if (CmbEdge.SelectedIndex >= 0) s.Edge = (DockEdge)CmbEdge.SelectedIndex;
            if (CmbCoexist.SelectedIndex >= 0) s.Coexist = (CoexistMode)CmbCoexist.SelectedIndex;
            s.AutoStartWithWindows = ChkAutoStart.IsChecked == true;

            // 触发主窗口即刻刷新视觉与位置
            _mainWindow.ApplySettingsAppearance();
            _mainWindow.ApplyDockPosition();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ApplyLivePreview();
            _viewModel.SaveSettings();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // 恢复初始配置
            var s = _viewModel.Settings;
            s.BackgroundColorHex = _originalSettings.BackgroundColorHex;
            s.BackgroundOpacity = _originalSettings.BackgroundOpacity;
            s.EnableAcrylicBlur = _originalSettings.EnableAcrylicBlur;
            s.IconSize = _originalSettings.IconSize;
            s.CornerRadius = _originalSettings.CornerRadius;
            s.Edge = _originalSettings.Edge;
            s.Coexist = _originalSettings.Coexist;
            s.FloatLeft = _originalSettings.FloatLeft;
            s.FloatTop = _originalSettings.FloatTop;
            s.AutoStartWithWindows = _originalSettings.AutoStartWithWindows;

            _mainWindow.ApplySettingsAppearance();
            _mainWindow.ApplyDockPosition();

            DialogResult = false;
            Close();
        }

        private void BtnResetDefault_Click(object sender, RoutedEventArgs e)
        {
            ChkAcrylic.IsChecked = true;
            SliderOpacity.Value = 0.75;
            SliderIconSize.Value = 40;
            TxtBgColor.Text = "#0F172A";
            CmbEdge.SelectedIndex = (int)DockEdge.Bottom;
            CmbCoexist.SelectedIndex = (int)CoexistMode.Topmost;
            ChkAutoStart.IsChecked = false;
            ApplyLivePreview();
        }

        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.CheckUpdateManual();
        }
    }
}
