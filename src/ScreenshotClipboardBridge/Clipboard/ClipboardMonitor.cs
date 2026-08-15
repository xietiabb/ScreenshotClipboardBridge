using System.Windows.Forms;
using ScreenshotClipboardBridge.Native;

namespace ScreenshotClipboardBridge.Clipboard;

/// <summary>
/// 剪贴板变化监听器。
/// 采用 Windows 原生 API「AddClipboardFormatListener」，剪贴板一变化即收到
/// WM_CLIPBOARDUPDATE 消息——这是事件驱动，不是轮询，零 CPU 空转。
/// 监听载体是一个隐藏的 NativeWindow（无窗口界面，仅用于收消息）。
/// </summary>
public sealed class ClipboardMonitor : NativeWindow, IDisposable
{
    /// <summary>WM_CLIPBOARDUPDATE：剪贴板内容已变化的系统消息。</summary>
    private const int WmClipboardUpdate = 0x031D;

    /// <summary>剪贴板内容发生变化时触发（在主/UI 线程上）。</summary>
    public event EventHandler? ClipboardChanged;

    /// <summary>
    /// 注册监听。返回是否成功注册了原生监听器；
    /// 失败（极罕见）时调用方可退化为轮询模式。
    /// </summary>
    public bool Start()
    {
        var createParams = new CreateParams();
        CreateHandle(createParams);
        return NativeMethods.AddClipboardFormatListener(Handle);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmClipboardUpdate)
        {
            ClipboardChanged?.Invoke(this, EventArgs.Empty);
        }
        base.WndProc(ref m);
    }

    // NativeWindow.DestroyHandle() 是 public，重写时不能缩小访问级别。
    public override void DestroyHandle()
    {
        // 销毁前务必注销监听，避免悬空句柄。
        try { NativeMethods.RemoveClipboardFormatListener(Handle); }
        catch { /* 忽略：窗口销毁过程中句柄可能已失效 */ }
        base.DestroyHandle();
    }

    public void Dispose() => DestroyHandle();
}
