using ScreenshotClipboardBridge.Services;
using Xunit;

namespace ScreenshotClipboardBridge.Tests;

/// <summary>
/// 「最近截图记录」持久化测试：读写往返、缺失/损坏容错。
/// </summary>
public class RecentScreenshotStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scb-recent-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public RecentScreenshotStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "last-screenshot.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 可忽略 */ }
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var store = new RecentScreenshotStore(_path);
        var time = new DateTime(2026, 8, 15, 12, 45, 33, DateTimeKind.Utc);

        store.Save(new RecentScreenshotStore.Entry(@"C:\shots\2026-08-15_12-45-33_a82f31.png", time));
        RecentScreenshotStore.Entry? loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(@"C:\shots\2026-08-15_12-45-33_a82f31.png", loaded.Path);
        Assert.Equal(time, loaded.CreatedAtUtc);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNull()
    {
        var store = new RecentScreenshotStore(_path);

        Assert.Null(store.Load());
    }

    [Fact]
    public void Load_CorruptFile_ReturnsNull()
    {
        File.WriteAllText(_path, "{ not valid json !!!");

        Assert.Null(new RecentScreenshotStore(_path).Load());
    }

    [Fact]
    public void Save_OverwritesPreviousEntry()
    {
        var store = new RecentScreenshotStore(_path);
        store.Save(new RecentScreenshotStore.Entry(@"C:\shots\first.png", DateTime.UtcNow));
        store.Save(new RecentScreenshotStore.Entry(@"C:\shots\second.png", DateTime.UtcNow));

        Assert.Equal(@"C:\shots\second.png", store.Load()!.Path);
    }
}
