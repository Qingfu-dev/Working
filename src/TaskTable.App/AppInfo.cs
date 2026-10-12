using System.Reflection;

namespace TaskTable.App;

/// <summary>软件元信息（设置窗口「关于软件」的数据源）。</summary>
public static class AppInfo
{
    /// <summary>作者。</summary>
    public const string Author = "Qing_Fu.CN";

    /// <summary>
    /// 使用教程（视频）链接：留空表示教程尚未发布，界面显示「制作中」。
    /// 视频发布后把链接填在这里即可，无需改动界面代码。
    /// </summary>
    public const string TutorialUrl = "";

    /// <summary>是否已配置教程链接。</summary>
    public static bool HasTutorialUrl => !string.IsNullOrWhiteSpace(TutorialUrl);

    /// <summary>版本号文本（如 v0.1.4），取自程序集版本（csproj 的 &lt;Version&gt;）。</summary>
    public static string VersionText { get; } = BuildVersionText();

    private static string BuildVersionText()
    {
        var v = typeof(AppInfo).Assembly.GetName().Version;
        return v is null ? "v0.1.4" : $"v{v.Major}.{v.Minor}.{v.Build}";
    }
}