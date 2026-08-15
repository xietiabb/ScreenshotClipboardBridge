using ScreenshotClipboardBridge.Core;
using Xunit;

namespace ScreenshotClipboardBridge.Tests;

/// <summary>
/// 防死循环守卫测试：识别自写路径；时间窗口过期后放行；
/// 期间若剪贴板变成新图片（快速连续截图）绝不误伤。
/// </summary>
public class LoopGuardTests
{
    private static (LoopGuard Guard, FakeClock Clock) Create()
    {
        var clock = new FakeClock();
        return (new LoopGuard(() => clock.Now), clock);
    }

    /// <summary>可手动推进的假时钟。</summary>
    private sealed class FakeClock
    {
        public DateTime Now { get; private set; } = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
        public void Advance(TimeSpan span) => Now += span;
    }

    [Fact]
    public void SelfWrite_IsDetected_WhenTextMatchesRecentWrite()
    {
        var (guard, clock) = Create();
        guard.MarkSelfWrite(@"C:\shots\2026-08-15_12-45-33_a82f31.png");

        var fake = new FakeClipboardSource { HasText = true, Text = @"C:\shots\2026-08-15_12-45-33_a82f31.png" };

        Assert.True(guard.IsSelfWrite(fake));
    }

    [Fact]
    public void SelfWrite_Expires_AfterWindow()
    {
        var (guard, clock) = Create();
        guard.MarkSelfWrite(@"C:\shots\a.png");

        clock.Advance(TimeSpan.FromSeconds(4)); // 窗口为 3 秒，4 秒后必然过期

        var fake = new FakeClipboardSource { HasText = true, Text = @"C:\shots\a.png" };
        Assert.False(guard.IsSelfWrite(fake));
    }

    [Fact]
    public void ImageClipboard_IsNeverBlocked_EvenInsideWindow()
    {
        var (guard, clock) = Create();
        guard.MarkSelfWrite(@"C:\shots\a.png");

        // 窗口内剪贴板却是图片（用户又截图了）→ 不是自写事件
        var fake = new FakeClipboardSource { HasImage = true, PngBytes = new byte[] { 1 } };

        Assert.False(guard.IsSelfWrite(fake));
    }

    [Fact]
    public void DifferentText_IsNotSelfWrite()
    {
        var (guard, clock) = Create();
        guard.MarkSelfWrite(@"C:\shots\a.png");

        var fake = new FakeClipboardSource { HasText = true, Text = "hello world" };

        Assert.False(guard.IsSelfWrite(fake));
    }

    [Fact]
    public void NoPriorWrite_IsNotSelfWrite()
    {
        var (guard, clock) = Create();

        var fake = new FakeClipboardSource { HasText = true, Text = "anything" };

        Assert.False(guard.IsSelfWrite(fake));
    }
}
