using Microsoft.Win32;
using ScreenshotClipboardBridge.App;

namespace ScreenshotClipboardBridge.Services;

/// <summary>
/// 开机自启服务。
/// 实现方式：注册表 Run 键（HKCU\Software\Microsoft\Windows\CurrentVersion\Run）。
/// 这是托盘类小工具最轻量的自启方案——无需管理员权限、无需计划任务。
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenshotClipboardBridge";

    /// <summary>当前是否已注册开机自启（以注册表为准）。</summary>
    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>设置/取消开机自启。</summary>
    public static void SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (enabled)
            {
                // 值带引号，防止路径含空格时注册表解析错误。
                key.SetValue(ValueName, $"\"{AppPaths.CurrentExePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // 注册表写入失败时静默降级（如策略限制），不影响程序运行。
        }
    }
}
