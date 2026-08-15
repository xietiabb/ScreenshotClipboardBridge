using ScreenshotClipboardBridge.Core;

namespace ScreenshotClipboardBridge.Services;

/// <summary>
/// 缓存自动清理服务。
/// 启动后延迟执行一次，之后按固定周期（默认 1 小时）清理过期截图。
/// 清理策略完全委托给 ScreenshotStore.Cleanup：只删本程序命名的文件。
/// </summary>
public sealed class RetentionService : IDisposable
{
    /// <summary>默认清理周期。</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(1);

    /// <summary>启动后延迟多久执行首次清理（错开启动峰值）。</summary>
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromSeconds(20);

    private readonly ScreenshotStore _store;
    private readonly Func<int> _getRetentionDays;
    private readonly System.Threading.Timer _timer;
    private bool _disposed;

    /// <summary>最近一次清理删除的文件数（供托盘/日志展示）。</summary>
    public int LastCleanupCount { get; private set; }

    public RetentionService(ScreenshotStore store, Func<int> getRetentionDays, TimeSpan? interval = null)
    {
        _store = store;
        _getRetentionDays = getRetentionDays;
        _timer = new System.Threading.Timer(
            _ => RunOnce(),
            null,
            FirstRunDelay,
            interval ?? DefaultInterval);
    }

    /// <summary>立即执行一次清理（线程安全，可从托盘菜单/设置页调用）。</summary>
    public int RunOnce()
    {
        try
        {
            LastCleanupCount = _store.Cleanup(_getRetentionDays());
            return LastCleanupCount;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Dispose();
    }
}
