namespace ScreenshotClipboardBridge.App;

/// <summary>
/// 应用级路径常量与解析逻辑。
/// 数据根目录：%LOCALAPPDATA%\ScreenshotClipboardBridge
///   ├─ images\       截图默认保存目录
///   └─ config.json   配置文件
/// </summary>
public static class AppPaths
{
    /// <summary>应用数据根目录（LocalApplicationData 对应当前用户）。</summary>
    public static string RootDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScreenshotClipboardBridge");

    /// <summary>默认截图保存目录。</summary>
    public static string DefaultImageDir { get; } = Path.Combine(RootDir, "images");

    /// <summary>配置文件完整路径。</summary>
    public static string ConfigPath { get; } = Path.Combine(RootDir, "config.json");

    /// <summary>当前进程可执行文件路径（发布后即 EXE 路径）。
    /// 单文件发布下 Assembly.Location 为空，因此以 ProcessPath 为准。</summary>
    public static string CurrentExePath { get; } =
        Environment.ProcessPath
        ?? Path.Combine(AppContext.BaseDirectory, "ScreenshotClipboardBridge.exe");

    /// <summary>
    /// 把配置中的 saveDirectory 解析为实际目录。
    /// "default" / 空字符串 → 默认目录；否则使用用户自定义的绝对路径。
    /// </summary>
    public static string ResolveSaveDirectory(string configured)
        => string.IsNullOrWhiteSpace(configured) || string.Equals(configured, "default", StringComparison.OrdinalIgnoreCase)
            ? DefaultImageDir
            : configured;
}
