namespace ScreenshotClipboardBridge.App;

/// <summary>
/// 极简文件日志（%LOCALAPPDATA%\ScreenshotClipboardBridge\app.log）。
/// 仅记录启动状态与异常/关键事件，正常运行时写入量极小，不影响性能。
/// 任何写入失败都静默忽略（日志是辅助，绝不能拖垮主流程）。
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();

    public static string LogPath { get; } = Path.Combine(AppPaths.RootDir, "app.log");

    public static void Write(string category, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.RootDir);
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] [{category}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 忽略：日志不可用不影响功能
        }
    }
}
