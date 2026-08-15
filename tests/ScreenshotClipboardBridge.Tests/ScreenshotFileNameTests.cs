using ScreenshotClipboardBridge.Core;
using Xunit;

namespace ScreenshotClipboardBridge.Tests;

/// <summary>
/// 文件名规则测试：命名格式、唯一性、清理白名单识别。
/// </summary>
public class ScreenshotFileNameTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 45, 33, DateTimeKind.Local);

    [Fact]
    public void GeneratedName_MatchesExpectedPattern()
    {
        string name = ScreenshotFileName.Generate(Now);

        // 形如 2026-08-15_12-45-33_a82f31.png
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_[0-9a-f]{6}\.png$", name);
        Assert.StartsWith("2026-08-15_12-45-33_", name);
        Assert.EndsWith(".png", name);
        Assert.True(ScreenshotFileName.IsOwnFile(name));
    }

    [Fact]
    public void GenerateUnique_ProducesUniqueNames_ForManyCalls()
    {
        string dir = Path.Combine(Path.GetTempPath(), "scb-fn-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 100; i++)
            {
                string name = ScreenshotFileName.GenerateUnique(dir, Now);
                Assert.True(names.Add(name), $"第 {i} 次生成出现重名: {name}");
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("notes.txt", false)]
    [InlineData("user-2026-08-15_12-45-33_a82f31.png", false)] // 非本程序前缀命名
    [InlineData("2026-08-15_12-45-33_zzzzzz.png", false)]        // 非法十六进制
    [InlineData("20260815_124533_ab12cd.png", false)]            // 分隔符错误
    [InlineData("2026-08-15_12-45-33_a82f31.png", true)]         // 合法
    [InlineData("2026-08-15_12-45-33_000000.png", true)]         // 合法（全零十六进制）
    public void IsOwnFile_OnlyAcceptsOurNamingPattern(string fileName, bool expected)
        => Assert.Equal(expected, ScreenshotFileName.IsOwnFile(fileName));
}
