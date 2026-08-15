using System.Runtime.InteropServices;

namespace ScreenshotClipboardBridge.Native;

/// <summary>
/// Win32 P/Invoke 集中声明。
/// 按功能分区注释，方便后续扩展（托盘、剪贴板、系统 API 等）。
/// </summary>
internal static class NativeMethods
{
    // ==================== 剪贴板监听 ====================

    /// <summary>
    /// 注册窗口接收 WM_CLIPBOARDUPDATE 消息（剪贴板内容变化通知）。
    /// 事件驱动、无轮询，是 Win32 官方推荐的剪贴板监听方式。
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    /// <summary>注销剪贴板监听（窗口销毁前必须调用）。</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
}
