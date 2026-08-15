using ScreenshotClipboardBridge.Services;
using Xunit;

namespace ScreenshotClipboardBridge.Tests;

/// <summary>
/// 配置持久化测试：默认值、读写往返、损坏文件容错、非法值规整。
/// </summary>
public class ConfigServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "scb-config-" + Guid.NewGuid().ToString("N"));
    private readonly string _path;

    public ConfigServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "config.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 可忽略 */ }
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileMissing()
    {
        var service = new ConfigService(_path);
        Config config = service.Load();

        Assert.True(config.Enabled);
        Assert.Equal("default", config.SaveDirectory);
        Assert.Equal(7, config.RetentionDays);
        Assert.True(config.Notification);
        Assert.False(config.Startup);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var service = new ConfigService(_path);
        service.Save(new Config
        {
            Enabled = false,
            SaveDirectory = @"C:\Users\me\Pictures\shots",
            RetentionDays = 30,
            Notification = false,
            Startup = true,
        });

        Config loaded = service.Load();

        Assert.False(loaded.Enabled);
        Assert.Equal(@"C:\Users\me\Pictures\shots", loaded.SaveDirectory);
        Assert.Equal(30, loaded.RetentionDays);
        Assert.False(loaded.Notification);
        Assert.True(loaded.Startup);
    }

    [Fact]
    public void Load_CorruptJson_FallsBackToDefaults()
    {
        File.WriteAllText(_path, "{ this is not valid json !!!");

        Config config = new ConfigService(_path).Load();

        Assert.True(config.Enabled);
        Assert.Equal(7, config.RetentionDays);
    }

    [Fact]
    public void Save_SanitizesInvalidRetentionToSeven()
    {
        var service = new ConfigService(_path);
        service.Save(new Config { RetentionDays = 999 });

        Assert.Equal(7, service.Load().RetentionDays);
    }
}
