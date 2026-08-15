namespace ScreenshotClipboardBridge.Clipboard;

/// <summary>
/// 剪贴板内容快照的只读抽象。
/// 引入接口是为了让「图片处理管线」可以脱离真实剪贴板做单元测试（注入 Fake 实现）。
/// </summary>
public interface IClipboardSource
{
    /// <summary>剪贴板中是否存在图片数据（Bitmap / DIB / PNG / JFIF 任一种）。</summary>
    bool HasImage { get; }

    /// <summary>剪贴板中是否存在文件列表（CF_HDROP，即用户复制了文件）。</summary>
    bool HasFileDrop { get; }

    /// <summary>剪贴板中是否存在文本。</summary>
    bool HasText { get; }

    /// <summary>剪贴板中的文本是否与指定字符串完全相等（用于识别「程序自己写入的路径」）。</summary>
    bool TextEquals(string? text);

    /// <summary>
    /// 尝试取出 PNG 编码的原始字节。
    /// 优先读取剪贴板自带的 "PNG" 格式（保留截图工具的原始编码、速度最快）；
    /// 否则回退为 Bitmap/DIB → 重新编码为 PNG。
    /// 返回 null 表示无法取得图片数据。
    /// </summary>
    byte[]? TryGetPngBytes();
}
