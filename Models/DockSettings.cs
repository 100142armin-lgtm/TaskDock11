using System;

namespace TaskDock11.Models
{
    public enum DockEdge
    {
        Float = 0,
        Bottom = 1,
        Top = 2,
        Left = 3,
        Right = 4
    }

    public enum CoexistMode
    {
        ReserveWorkArea = 0, // 挤占工作区避让
        Topmost = 1,         // 始终置顶
        AutoHide = 2         // 自动隐藏
    }

    public class DockSettings
    {
        // 视觉外观
        public string BackgroundColorHex { get; set; } = "#0F172A";
        public double BackgroundOpacity { get; set; } = 0.75;
        public bool EnableAcrylicBlur { get; set; } = true;
        public double CornerRadius { get; set; } = 16.0;
        public double IconSize { get; set; } = 40.0; // 默认 40px，范围 28~64

        // 停靠与行为
        public DockEdge Edge { get; set; } = DockEdge.Bottom;
        public CoexistMode Coexist { get; set; } = CoexistMode.Topmost;
        public double FloatLeft { get; set; } = 300;
        public double FloatTop { get; set; } = 800;

        // 系统设置
        public bool AutoStartWithWindows { get; set; } = false;

        // 团队与更新
        public string GitHubRepoOwner { get; set; } = "100142armin-lgtm";
        public string GitHubRepoName { get; set; } = "TaskDock11";
        public string CurrentVersion { get; set; } = "v1.0.0";
    }
}
