using ScreenshotClipboardBridge.Clipboard;

namespace ScreenshotClipboardBridge.Core;

/// <summary>
/// 防死循环守卫。
///
/// 问题：程序把「路径文本」写回剪贴板后，剪贴板变化事件会再次触发。
/// 虽然处理管线只认「图片」格式（文本事件天然被过滤），但为了绝对安全，
/// 这里再显式记录「最近一次由本程序写入的路径」，在时间窗口内识别并跳过。
///
/// 关键设计：判定条件是「无图片 + 有文本 + 文本与上次自写路径一致」，
/// 而不是简单的时间戳拦截——这样用户连续快速截两张图（1 秒内）也不会被误伤。
/// </summary>
public sealed class LoopGuard
{
    /// <summary>自写路径的有效时间窗口。超过该窗口不再认为是「自写事件」。</summary>
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private readonly Func<DateTime> _utcNow;

    private DateTime _lastWriteUtc = DateTime.MinValue;
    private string? _lastPath;

    /// <summary>构造；<paramref name="utcNow"/> 用于注入时钟（测试时间窗口用）。</summary>
    public LoopGuard(Func<DateTime>? utcNow = null) => _utcNow = utcNow ?? (() => DateTime.UtcNow);

    /// <summary>
    /// 记录一次程序自写的剪贴板文本（在写入剪贴板之后立即调用）。
    /// </summary>
    public void MarkSelfWrite(string path)
    {
        _lastWriteUtc = _utcNow();
        _lastPath = path;
    }

    /// <summary>
    /// 判断当前剪贴板快照是否就是「程序自己刚写入的路径」。
    /// </summary>
    public bool IsSelfWrite(IClipboardSource source)
    {
        if (_lastPath is null)
        {
            return false;
        }

        // 超出时间窗口：不再拦截（此时剪贴板内容已被用户的新操作覆盖）。
        if (_utcNow() - _lastWriteUtc > Window)
        {
            return false;
        }

        // 窗口内：必须是「纯文本且内容等于自写路径」才算自写事件。
        // （若剪贴板现在又变成了图片，说明用户又截图了，绝不拦截。）
        return !source.HasImage && source.HasText && source.TextEquals(_lastPath);
    }
}
