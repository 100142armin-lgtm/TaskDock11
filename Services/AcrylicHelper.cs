using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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

        public static void EnableBlur(Window window, bool enable, uint gradientColor = 0x99000000)
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
            catch
            {
                // 旧版本降级走 AccentPolicy
            }

            // 2. 备用高兼容性 AccentPolicy (支持 Win10 及 Win11 全版本)
            try
            {
                var policy = new AccentPolicy
                {
                    AccentState = enable ? AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND : AccentState.ACCENT_DISABLED,
                    AccentFlags = 2,
                    GradientColor = (int)gradientColor
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
            catch
            {
                // 忽略底层失败
            }
        }
    }
}
