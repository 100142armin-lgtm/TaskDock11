using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TaskDock11.Models;
using TaskDock11.Services.Win32;

namespace TaskDock11.Services
{
    public class AppBarService
    {
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _isRegistered = false;

        public void Register(Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.Handle;
            if (_hwnd == IntPtr.Zero || _isRegistered) return;

            var data = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.APPBARDATA)),
                hWnd = _hwnd,
                uCallbackMessage = 0x8000 + 100 // 自定义回调消息
            };

            NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref data);
            _isRegistered = true;
        }

        public void Unregister()
        {
            if (!_isRegistered || _hwnd == IntPtr.Zero) return;

            var data = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.APPBARDATA)),
                hWnd = _hwnd
            };

            NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref data);
            _isRegistered = false;
        }

        public void SetDockEdge(DockEdge edge, int width, int height)
        {
            if (!_isRegistered || _hwnd == IntPtr.Zero || edge == DockEdge.Float)
            {
                Unregister();
                return;
            }

            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            int uEdge = NativeMethods.ABE_BOTTOM;
            var rc = new NativeMethods.RECT();

            switch (edge)
            {
                case DockEdge.Top:
                    uEdge = NativeMethods.ABE_TOP;
                    rc.left = 0;
                    rc.top = 0;
                    rc.right = screenWidth;
                    rc.bottom = height;
                    break;
                case DockEdge.Bottom:
                    uEdge = NativeMethods.ABE_BOTTOM;
                    rc.left = 0;
                    rc.top = screenHeight - height;
                    rc.right = screenWidth;
                    rc.bottom = screenHeight;
                    break;
                case DockEdge.Left:
                    uEdge = NativeMethods.ABE_LEFT;
                    rc.left = 0;
                    rc.top = 0;
                    rc.right = width;
                    rc.bottom = screenHeight;
                    break;
                case DockEdge.Right:
                    uEdge = NativeMethods.ABE_RIGHT;
                    rc.left = screenWidth - width;
                    rc.top = 0;
                    rc.right = screenWidth;
                    rc.bottom = screenHeight;
                    break;
            }

            var data = new NativeMethods.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.APPBARDATA)),
                hWnd = _hwnd,
                uEdge = (uint)uEdge,
                rc = rc
            };

            // 查询并设置AppBar位置，系统会自动挤占并保留工作区
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref data);
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref data);
        }
    }
}
