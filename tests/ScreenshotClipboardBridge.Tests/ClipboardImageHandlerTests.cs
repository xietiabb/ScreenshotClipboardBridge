using System.Drawing;
using System.Drawing.Imaging;
using ScreenshotClipboardBridge.Clipboard;
using ScreenshotClipboardBridge.Core;
using Xunit;

namespace ScreenshotClipboardBridge.Tests;

/// <summary>
/// 核心管线测试：图片 → 保存 PNG → 路径写回剪贴板。
/// 覆盖验收标准中的关键场景：只处理图片、不处理文本/代码/文件、
/// 防死循环、连续 10 次截图不丢失不重复。
/// </summary>
public class ClipboardImageHandlerTests
{
    /// <summary>临时目录工厂：每个测试独立目录，用后清理。</summary>
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scb-tests", Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* 清理失败可忽略 */ }
        }
    }

    private static (ClipboardImageHandler Handler, ScreenshotStore Store, List<string> WrittenPaths) CreateHandler(TempDir dir, LoopGuard? guard = null)
    {
        var store = new ScreenshotStore(() => dir.Path);
        var written = new List<string>();
        var handler = new ClipboardImageHandler(store, guard ?? new LoopGuard(), written.Add);
        return (handler, store, written);
    }

    /// <summary>生成一段真实可解码的 PNG 字节（1x1 或指定尺寸）。</summary>
    private static byte[] MakePng(int size = 16)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.DodgerBlue);
        }
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    // ---------- Test 1：截图自动保存，路径进入剪贴板 ----------

    [Fact]
    public void Screenshot_IsSaved_AndPathWrittenBack()
    {
        using var dir = new TempDir();
        var (handler, store, written) = CreateHandler(dir);
        var fake = new FakeClipboardSource { HasImage = true, PngBytes = MakePng() };

        bool result = handler.TryConvert(fake);

        Assert.True(result);
        Assert.Single(written);
        Assert.EndsWith(".png", written[0]);
        Assert.True(File.Exists(written[0]), "保存的 PNG 文件应存在于磁盘");
        Assert.Equal(handler.LastSavedPath, written[0]);
        Assert.Single(Directory.EnumerateFiles(dir.Path, "*.png"));
    }

    // ---------- Test 2：连续 10 次截图 → 10 个不同文件，不丢不重 ----------

    [Fact]
    public void TenConsecutiveScreenshots_ProduceTenUniqueFiles()
    {
        using var dir = new TempDir();
        var (handler, store, written) = CreateHandler(dir);

        for (int i = 0; i < 10; i++)
        {
            var fake = new FakeClipboardSource { HasImage = true, PngBytes = MakePng(8 + i) };
            Assert.True(handler.TryConvert(fake), $"第 {i + 1} 张截图应转换成功");
        }

        var files = Directory.EnumerateFiles(dir.Path, "*.png").ToArray();
        Assert.Equal(10, files.Length);
        Assert.True(files.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 10, "文件名不得重复");
        Assert.Equal(10, written.Count);
        Assert.True(written.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 10, "写回剪贴板的路径不得重复");
    }

    // ---------- Test 3/4：普通文本、代码 → 完全不处理 ----------

    [Theory]
    [InlineData("hello")]
    [InlineData("print(\"hello\")")]
    [InlineData("const x = 1; // code")]
    [InlineData("")]
    public void PlainTextOrCode_IsNeverProcessed(string text)
    {
        using var dir = new TempDir();
        var (handler, store, written) = CreateHandler(dir);
        var fake = new FakeClipboardSource { HasText = true, Text = text };

        Assert.False(handler.TryConvert(fake));
        Assert.Empty(written);
        Assert.Empty(Directory.EnumerateFiles(dir.Path, "*.png"));
    }

    // ---------- Test 5：复制文件（含图片文件）→ 完全不处理 ----------

    [Fact]
    public void FileCopy_IsNeverProcessed_EvenWithImageFormat()
    {
        using var dir = new TempDir();
        var (handler, store, written) = CreateHandler(dir);

        // 关键场景：资源管理器里复制图片文件时，剪贴板可能同时带图片格式与文件列表
        var fake = new FakeClipboardSource
        {
            HasImage = true,   // 图片格式存在
            HasFileDrop = true, // 但用户复制的是「文件」
            PngBytes = MakePng(),
        };

        Assert.False(handler.TryConvert(fake), "复制文件必须放行");
        Assert.Empty(written);
        Assert.Empty(Directory.EnumerateFiles(dir.Path, "*.png"));
    }

    // ---------- Test 6：防死循环 ----------

    [Fact]
    public void SelfWrittenPath_IsRecognized_AndSkipped()
    {
        using var dir = new TempDir();
        var guard = new LoopGuard();
        var (handler, store, written) = CreateHandler(dir, guard);

        // 模拟：程序刚把路径写回剪贴板，随后收到剪贴板变化事件
        string selfPath = System.IO.Path.Combine(dir.Path, "2026-08-15_12-45-33_a82f31.png");
        guard.MarkSelfWrite(selfPath);

        var fake = new FakeClipboardSource { HasText = true, Text = selfPath };

        Assert.False(handler.TryConvert(fake), "程序自写的路径不得再次处理");
        Assert.Empty(written);
        Assert.Empty(Directory.EnumerateFiles(dir.Path, "*.png"));
    }

    [Fact]
    public void FastSecondScreenshot_IsNotBlockedByGuard()
    {
        using var dir = new TempDir();
        var guard = new LoopGuard();
        var (handler, store, written) = CreateHandler(dir, guard);

        // 程序刚写过路径，但用户 1 秒内又截了一张图（剪贴板是图片）→ 必须处理
        guard.MarkSelfWrite(System.IO.Path.Combine(dir.Path, "old.png"));

        var fake = new FakeClipboardSource { HasImage = true, PngBytes = MakePng() };

        Assert.True(handler.TryConvert(fake), "连续快速截图不能被防循环逻辑误伤");
        Assert.Single(written);
    }

    // ---------- 边界：剪贴板无图片字节 → 不处理 ----------

    [Fact]
    public void ImageFormatWithoutBytes_IsNotProcessed()
    {
        using var dir = new TempDir();
        var (handler, store, written) = CreateHandler(dir);
        var fake = new FakeClipboardSource { HasImage = true, PngBytes = null };

        Assert.False(handler.TryConvert(fake));
        Assert.Empty(written);
    }
}
