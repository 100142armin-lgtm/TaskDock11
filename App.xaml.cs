using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace TaskDock11;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    static App()
    {
        try
        {
            // 预加载关键 WPF 原生依赖，彻底杜绝单文件/宿主运行时下的 SetWindowLongPtrWndProc DllNotFoundException
            string[] criticalDlls = { "wpfgfx_cor3.dll", "PresentationNative_cor3.dll", "D3DCompiler_47_cor3.dll", "vcruntime140_cor3.dll" };

            // 1. 优先从当前应用程序目录预加载
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var dll in criticalDlls)
            {
                string localFile = Path.Combine(baseDir, dll);
                if (File.Exists(localFile))
                {
                    NativeLibrary.TryLoad(localFile, out _);
                }
            }

            // 2. 从系统的 Microsoft.WindowsDesktop.App 共享目录预加载
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string desktopRoot = Path.Combine(progFiles, "dotnet", "shared", "Microsoft.WindowsDesktop.App");
            if (Directory.Exists(desktopRoot))
            {
                var versionDirs = Directory.GetDirectories(desktopRoot).OrderByDescending(d => d);
                foreach (var vDir in versionDirs)
                {
                    foreach (var dll in criticalDlls)
                    {
                        string fullPath = Path.Combine(vDir, dll);
                        if (File.Exists(fullPath))
                        {
                            NativeLibrary.TryLoad(fullPath, out _);
                        }
                    }
                }
            }
        }
        catch { }
    }

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.WriteAllText(logPath, e.ExceptionObject?.ToString() ?? "Unknown exception");
            }
            catch { }
        };

        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.WriteAllText(logPath, e.Exception?.ToString() ?? "Unknown dispatcher exception");
            }
            catch { }
        };
    }
}

