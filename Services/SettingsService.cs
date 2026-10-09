using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using TaskDock11.Models;

namespace TaskDock11.Services
{
    public class SettingsService
    {
        private static readonly string AppFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskDock11");
        private static readonly string SettingsPath = Path.Combine(AppFolder, "settings.json");

        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppRegistryName = "TaskDock11";

        public DockSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<DockSettings>(json);
                    if (settings != null) return settings;
                }
            }
            catch { }

            return new DockSettings();
        }

        public void SaveSettings(DockSettings settings)
        {
            try
            {
                if (!Directory.Exists(AppFolder))
                {
                    Directory.CreateDirectory(AppFolder);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsPath, json);

                // 更新开机自启动状态
                SetAutoStart(settings.AutoStartWithWindows);
            }
            catch { }
        }

        public void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
                if (key == null) return;

                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath)) return;

                if (enable)
                {
                    key.SetValue(AppRegistryName, $"\"{exePath}\"");
                }
                else
                {
                    if (key.GetValue(AppRegistryName) != null)
                    {
                        key.DeleteValue(AppRegistryName, false);
                    }
                }
            }
            catch { }
        }
    }
}
