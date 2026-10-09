using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaskDock11.Services
{
    public static class LogoGenerator
    {
        public static DrawingImage CreateTechLogo()
        {
            var group = new DrawingGroup();

            // 1. 底层极光发光渐变光晕
            var glowBrush = new RadialGradientBrush(
                Color.FromArgb(140, 0, 210, 255),
                Color.FromArgb(0, 0, 0, 0))
            {
                RadiusX = 0.5,
                RadiusY = 0.5,
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5)
            };
            group.Children.Add(new GeometryDrawing(glowBrush, null, new EllipseGeometry(new Point(64, 64), 58, 58)));

            // 2. 外层科技流光环 (Cyan -> Violet 渐变)
            var ringGradient = new LinearGradientBrush(
                Color.FromRgb(0, 229, 255),
                Color.FromRgb(168, 85, 247),
                new Point(0, 0),
                new Point(1, 1));

            var ringPen = new Pen(ringGradient, 4.5);
            group.Children.Add(new GeometryDrawing(null, ringPen, new EllipseGeometry(new Point(64, 64), 44, 44)));

            // 3. 内层悬浮胶囊基座 (Dock 意向)
            var dockGradient = new LinearGradientBrush(
                Color.FromArgb(220, 20, 25, 45),
                Color.FromArgb(240, 10, 15, 30),
                new Point(0, 0),
                new Point(0, 1));
            var dockPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 229, 255)), 1.5);
            var dockRect = new Rect(24, 68, 80, 24);
            group.Children.Add(new GeometryDrawing(dockGradient, dockPen, new RectangleGeometry(dockRect, 8, 8)));

            // 4. 基座上的三个科技脉冲晶体 (三种窗口象征，极光蓝、星空紫、亮青)
            var c1 = new Rect(34, 46, 14, 18);
            var c2 = new Rect(56, 38, 16, 26);
            var c3 = new Rect(80, 46, 14, 18);

            var b1 = new LinearGradientBrush(Color.FromRgb(0, 229, 255), Color.FromRgb(0, 150, 255), 45);
            var b2 = new LinearGradientBrush(Color.FromRgb(217, 70, 239), Color.FromRgb(147, 51, 234), 45);
            var b3 = new LinearGradientBrush(Color.FromRgb(56, 189, 248), Color.FromRgb(99, 102, 241), 45);

            group.Children.Add(new GeometryDrawing(b1, null, new RectangleGeometry(c1, 3, 3)));
            group.Children.Add(new GeometryDrawing(b2, null, new RectangleGeometry(c2, 4, 4)));
            group.Children.Add(new GeometryDrawing(b3, null, new RectangleGeometry(c3, 3, 3)));

            // 5. 核心激光反光微点
            var whitePen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 1.5);
            group.Children.Add(new GeometryDrawing(null, whitePen, new LineGeometry(new Point(58, 42), new Point(62, 42))));

            return new DrawingImage(group);
        }
    }
}
