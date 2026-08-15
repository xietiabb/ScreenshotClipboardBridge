namespace ScreenshotClipboardBridge.Services;

/// <summary>
/// 应用配置模型。JSON 字段使用 camelCase，与配置文件的键一一对应。
/// </summary>
public sealed class Config
{
    /// <summary>总开关：是否自动把剪贴板截图转换为文件路径。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>截图保存目录；"default" 表示 %LOCALAPPDATA%\ScreenshotClipboardBridge\images。</summary>
    public string SaveDirectory { get; set; } = "default";

    /// <summary>截图保留天数；0 = 永久保存；可选 1 / 3 / 7 / 30。</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>转换成功时是否显示系统通知。</summary>
    public bool Notification { get; set; } = true;

    /// <summary>是否开机自动启动（同时写入注册表 Run 键）。</summary>
    public bool Startup { get; set; } = false;
}
