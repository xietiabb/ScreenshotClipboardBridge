using ScreenshotClipboardBridge.Core;
using Xunit;

namespace ScreenshotClipboardBridge.Tests;

/// <summary>
/// 存储与清理策略测试：
///  - 保存文件、文件名唯一；
///  - 按保留天数清理过期截图；
///  - 绝不删除非本程序创建的文件（安全红线）。
/// </summary>
public class ScreenshotStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scb-store-" + Guid.NewGuid().ToString("N"));
    private readonly ScreenshotStore _store;

    public ScreenshotStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new ScreenshotStore(() => _dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 可忽略 */ }
    }

    /// <summary>制造一个「本程序命名」的旧文件（创建时间被改到指定时间）。</summary>
    private string CreateOwnFile(string fileName, DateTime creationTime)
    {
        string path = Path.Combine(_dir, fileName);
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        File.SetCreationTime(path, creationTime);
        return path;
    }

    [Fact]
    public void Save_ReturnsAbsolutePath_AndCreatesFile()
    {
        string path = _store.Save(new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        Assert.True(Path.IsPathFullyQualified(path));
        Assert.EndsWith(".png", path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_TenTimes_AllUnique()
    {
        var paths = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            paths.Add(_store.Save(new byte[] { (byte)i, 2, 3 }));
        }

        Assert.Equal(10, paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(10, Directory.EnumerateFiles(_dir, "*.png").Count());
    }

    [Fact]
    public void Cleanup_DeletesOnlyExpiredOwnFiles()
    {
        // 7 天前创建（过期）
        CreateOwnFile("2026-08-01_10-00-00_a1b2c3.png", DateTime.Now.AddDays(-7));
        // 1 天前创建（不过期）
        CreateOwnFile("2026-08-14_10-00-00_d4e5f6.png", DateTime.Now.AddDays(-1));
        // 用户自己的旧文件（即使很旧也绝不能删）
        string foreign = Path.Combine(_dir, "my-important-notes.png");
        File.WriteAllText(foreign, "user data");
        File.SetCreationTime(foreign, DateTime.Now.AddDays(-30));

        int deleted = _store.Cleanup(retentionDays: 7);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(Path.Combine(_dir, "2026-08-01_10-00-00_a1b2c3.png")));
        Assert.True(File.Exists(Path.Combine(_dir, "2026-08-14_10-00-00_d4e5f6.png")));
        Assert.True(File.Exists(foreign), "用户自己的文件必须原样保留");
    }

    [Fact]
    public void Cleanup_Permanent_DeletesNothing()
    {
        CreateOwnFile("2026-01-01_10-00-00_a1b2c3.png", DateTime.Now.AddDays(-100));

        int deleted = _store.Cleanup(retentionDays: 0); // 永久保存

        Assert.Equal(0, deleted);
        Assert.Single(Directory.EnumerateFiles(_dir, "*.png"));
    }

    [Fact]
    public void ClearAll_DeletesOnlyOwnFiles()
    {
        CreateOwnFile("2026-08-14_10-00-00_d4e5f6.png", DateTime.Now);
        string foreign = Path.Combine(_dir, "readme.png");
        File.WriteAllText(foreign, "not ours");

        int deleted = _store.ClearAll();

        Assert.Equal(1, deleted);
        Assert.DoesNotContain(Directory.EnumerateFiles(_dir, "*.png"), f => ScreenshotFileName.IsOwnFile(Path.GetFileName(f)));
        Assert.True(File.Exists(foreign));
    }
}
