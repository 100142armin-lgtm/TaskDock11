using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TaskDock11.Services.Win32;

namespace TaskDock11.Services
{
    public static class IconHelper
    {
        public static ImageSource? GetWindowIcon(IntPtr hWnd, int processId)
        {
            try
            {
                // 1. 尝试通过 WM_GETICON 消息获取 (大图标优先)
                IntPtr hIcon = IntPtr.Zero;
                NativeMethods.SendMessageTimeout(hWnd, NativeMethods.WM_GETICON, NativeMethods.ICON_BIG, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, 100, out hIcon);

                if (hIcon == IntPtr.Zero)
                {
                    NativeMethods.SendMessageTimeout(hWnd, NativeMethods.WM_GETICON, NativeMethods.ICON_SMALL2, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, 100, out hIcon);
                }

                if (hIcon == IntPtr.Zero)
                {
                    NativeMethods.SendMessageTimeout(hWnd, NativeMethods.WM_GETICON, NativeMethods.ICON_SMALL, IntPtr.Zero, NativeMethods.SMTO_ABORTIFHUNG, 100, out hIcon);
                }

                // 2. 尝试从窗口类 Class 中获取
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCLP_HICON);
                }
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = NativeMethods.GetClassLongPtr(hWnd, NativeMethods.GCLP_HICONSM);
                }

                if (hIcon != IntPtr.Zero)
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }

                // 3. 尝试从进程关联的 exe 路径提取图标
                if (processId > 0)
                {
                    try
                    {
                        using var process = Process.GetProcessById(processId);
                        string? exePath = process.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                            if (sysIcon != null)
                            {
                                var source = Imaging.CreateBitmapSourceFromHIcon(
                                    sysIcon.Handle,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                source.Freeze();
                                return source;
                            }
                        }
                    }
                    catch
                    {
                        // 忽略某些无权限进程的异常 (如系统保护进程)
                    }
                }
            }
            catch
            {
                // 兜底容错
            }

            return null;
        }
    }
}
