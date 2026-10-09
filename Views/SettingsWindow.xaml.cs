using System;
using System.Windows;
using System.Windows.Controls;
using TaskDock11.Models;
using TaskDock11.ViewModels;

namespace TaskDock11.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _viewModel;
        public bool SettingsSaved { get; private set; } = false;

        public SettingsWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            LoadCurrentSettings();
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
            TxtOwner.Text = s.GitHubRepoOwner;
            TxtRepo.Text = s.GitHubRepoName;
        }

        private void PresetColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                TxtBgColor.Text = hex;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var s = _viewModel.Settings;
            s.EnableAcrylicBlur = ChkAcrylic.IsChecked == true;
            s.BackgroundOpacity = SliderOpacity.Value;
            s.IconSize = SliderIconSize.Value;
            s.BackgroundColorHex = string.IsNullOrWhiteSpace(TxtBgColor.Text) ? "#1A1A1A" : TxtBgColor.Text.Trim();
            s.Edge = (DockEdge)Math.Max(0, CmbEdge.SelectedIndex);
            s.Coexist = (CoexistMode)Math.Max(0, CmbCoexist.SelectedIndex);
            s.AutoStartWithWindows = ChkAutoStart.IsChecked == true;
            s.GitHubRepoOwner = TxtOwner.Text.Trim();
            s.GitHubRepoName = TxtRepo.Text.Trim();

            _viewModel.SaveSettings();
            SettingsSaved = true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Settings.GitHubRepoOwner = TxtOwner.Text.Trim();
            _viewModel.Settings.GitHubRepoName = TxtRepo.Text.Trim();
            _viewModel.CheckUpdateManual();
        }
    }
}
