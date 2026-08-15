using ScreenshotClipboardBridge.Core;

namespace ScreenshotClipboardBridge.Clipboard;

/// <summary>
/// 核心处理管线：剪贴板图片 → 保存 PNG → 文件路径写回剪贴板。
/// 这是整个应用的心脏，也是「防死循环」「只处理图片」两条关键逻辑的所在。
///
/// 处理条件（顺序判断，全部满足才处理）：
///   1. 不是程序自己刚写入的路径（防死循环）；
///   2. 剪贴板含图片数据（HasImage）；
///   3. 不含文件列表（CF_HDROP）——复制文件（哪怕图片文件）一律放行，绝不劫持；
///   4. 不含文本——普通文本/代码/富文本复制一律放行。
/// </summary>
public sealed class ClipboardImageHandler
{
    private readonly ScreenshotStore _store;
    private readonly LoopGuard _guard;
    private readonly Action<string> _writePathToClipboard;

    /// <summary>最近一次成功保存的图片绝对路径（供托盘提示 / 后续 MCP 扩展读取）。</summary>
    public string? LastSavedPath { get; private set; }

    /// <summary>最近一次成功保存的时间（UTC）。</summary>
    public DateTime? LastSavedAtUtc { get; private set; }

    /// <summary>
    /// 程序启动时恢复「最近一次截图」记录（内存态，配合持久化存储跨重启保留）。
    /// </summary>
    public void RestoreLastSaved(string path, DateTime createdAtUtc)
    {
        LastSavedPath = path;
        LastSavedAtUtc = createdAtUtc;
    }

    public ClipboardImageHandler(ScreenshotStore store, LoopGuard guard, Action<string> writePathToClipboard)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _writePathToClipboard = writePathToClipboard ?? throw new ArgumentNullException(nameof(writePathToClipboard));
    }

    /// <summary>
    /// 尝试把当前剪贴板快照转换成文件路径。
    /// </summary>
    /// <param name="source">剪贴板快照。</param>
    /// <returns>true 表示已保存并写回路径；false 表示本次不处理（非图片/文本/文件/自写等）。</returns>
    public bool TryConvert(IClipboardSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // 1) 防死循环：识别程序自己写入的路径，直接跳过。
        if (_guard.IsSelfWrite(source))
        {
            return false;
        }

        // 2) 只处理图片：非图片、文件复制、文本复制全部放行。
        if (!source.HasImage || source.HasFileDrop || source.HasText)
        {
            return false;
        }

        // 3) 取 PNG 字节（原始 PNG 优先，否则重编码）。
        byte[]? pngBytes = source.TryGetPngBytes();
        if (pngBytes is null || pngBytes.Length == 0)
        {
            return false;
        }

        // 4) 保存到磁盘。
        string path = _store.Save(pngBytes);

        // 5) 把绝对路径以纯文本写回剪贴板（覆盖原图片，这正是用户要的效果）。
        _writePathToClipboard(path);

        // 6) 记录防循环标记（必须在写回之后立即记录）。
        _guard.MarkSelfWrite(path);

        LastSavedPath = path;
        LastSavedAtUtc = DateTime.UtcNow;
        return true;
    }
}
