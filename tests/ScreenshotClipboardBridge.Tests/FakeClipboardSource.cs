using ScreenshotClipboardBridge.Clipboard;

namespace ScreenshotClipboardBridge.Tests;

/// <summary>
/// 剪贴板快照的测试替身（Fake）：让核心管线可以在不触碰真实剪贴板的情况下做单元测试。
/// </summary>
public sealed class FakeClipboardSource : IClipboardSource
{
    public bool HasImage { get; set; }
    public bool HasFileDrop { get; set; }
    public bool HasText { get; set; }
    public string? Text { get; set; }
    public byte[]? PngBytes { get; set; }

    public bool TextEquals(string? text)
        => HasText && string.Equals(Text, text, StringComparison.Ordinal);

    public byte[]? TryGetPngBytes() => PngBytes;
}
