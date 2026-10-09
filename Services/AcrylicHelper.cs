using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TaskDock11.Services.Win32;

namespace TaskDock11.Services
{
    public static class AcrylicHelper
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        public static void EnableBlur(Window window, bool enable, Color? tintColor = null, double opacity = 0.75)
        {
            var helper = new WindowInteropHelper(window);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero) return;

            // 1. 尝试 Win11 官方 Backdrop API (22H2+)
            try
            {
                int backdropType = enable ? 3 : 1; // 3 = Acrylic
                NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
                
                int darkMode = 1;
                NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
            }
            catch { }

            // 2. 构造符合 Win10/Win11 AccentPolicy 的 GradientColor (格式: AABBGGRR)
            try
            {
                Color c = tintColor ?? Color.FromRgb(20, 20, 24);
                byte alpha = (byte)(Math.Clamp(opacity, 0.05, 1.0) * 255);
                
                // 注意 AccentPolicy 的字节顺序是 A A B B G G R R
                int rawGradient = (alpha << 24) | (c.B << 16) | (c.G << 8) | c.R;

                var policy = new AccentPolicy
                {
                    AccentState = enable ? AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND : AccentState.ACCENT_DISABLED,
                    AccentFlags = 2,
                    GradientColor = rawGradient
                };

                int structSize = Marshal.SizeOf(policy);
                IntPtr pPolicy = Marshal.AllocHGlobal(structSize);
                Marshal.StructureToPtr(policy, pPolicy, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = pPolicy,
                    SizeOfData = structSize
                };

                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(pPolicy);
            }
            catch { }
        }
    }
}
