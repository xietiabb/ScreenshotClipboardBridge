using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenshotClipboardBridge.Clipboard;

/// <summary>
/// 基于真实 Windows 剪贴板（System.Windows.Forms.Clipboard）的快照实现。
/// 说明：
///  - 所有读取都做了 try/catch：剪贴板可能被其他进程临时锁定（Excel/大文件复制等），
///    此时 WinForms 会抛 ExternalException，不能让它炸掉托盘进程。
///  - 只在「快照捕获」时访问一次剪贴板，后续判断全部基于内存中的 IDataObject。
/// </summary>
public sealed class WinClipboardSource : IClipboardSource
{
    /// <summary>PNG 自定义格式名（截图工具/部分应用会附带原始 PNG 数据）。</summary>
    private const string FormatPng = "PNG";

    /// <summary>JFIF 自定义格式名（JPEG 类图片）。</summary>
    private const string FormatJfif = "JFIF";

    private readonly IDataObject? _data;

    private WinClipboardSource(IDataObject? data) => _data = data;

    /// <summary>
    /// 捕获当前剪贴板快照。
    /// 若剪贴板被其他进程锁定，最多重试 5 次（每次间隔 80ms），最终仍失败则返回空快照。
    /// </summary>
    public static WinClipboardSource Capture()
    {
        IDataObject? data = null;
        for (int i = 0; i < 5; i++)
        {
            try
            {
                data = System.Windows.Forms.Clipboard.GetDataObject();
                break;
            }
            catch (ExternalException)
            {
                Thread.Sleep(80);
            }
        }
        return new WinClipboardSource(data);
    }

    public bool HasImage => Safe(() => _data is not null &&
        (_data.GetDataPresent(DataFormats.Bitmap)
         || _data.GetDataPresent(DataFormats.Dib)
         || _data.GetDataPresent(FormatPng)
         || _data.GetDataPresent(FormatJfif)));

    public bool HasFileDrop => Safe(() => _data?.GetDataPresent(DataFormats.FileDrop) == true);

    public bool HasText => Safe(() => _data?.GetDataPresent(DataFormats.UnicodeText) == true
        || _data?.GetDataPresent(DataFormats.Text) == true);

    public bool TextEquals(string? text)
    {
        if (text is null) return false;
        return Safe(() =>
        {
            string? current = _data?.GetData(DataFormats.UnicodeText) as string
                ?? _data?.GetData(DataFormats.Text) as string;
            return string.Equals(current, text, StringComparison.Ordinal);
        });
    }

    public byte[]? TryGetPngBytes()
    {
        if (_data is null) return null;

        // 1) 优先取原始 "PNG" 格式数据（截图工具的原始字节，零重编码损失）。
        try
        {
            switch (_data.GetData(FormatPng))
            {
                case byte[] raw:
                    return raw;
                case Stream stream:
                    using (stream)
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        return ms.ToArray();
                    }
            }
        }
        catch
        {
            // 落到 2) 重编码路径
        }

        // 2) 回退：Bitmap/DIB → 重新编码为 PNG。
        try
        {
            if (_data.GetData(DataFormats.Bitmap) is Image image)
            {
                using (image)
                using (var ms = new MemoryStream())
                {
                    image.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }
        catch
        {
            // 无法读取图片，返回 null
        }

        return null;
    }

    /// <summary>统一异常兜底：任何剪贴板访问异常都当作「不满足条件」处理。</summary>
    private static bool Safe(Func<bool> action)
    {
        try { return action(); }
        catch { return false; }
    }
}
