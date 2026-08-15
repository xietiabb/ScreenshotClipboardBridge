using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ScreenshotClipboardBridge.Core;

/// <summary>
/// 截图文件名生成与识别。
/// 命名规则：yyyy-MM-dd_HH-mm-ss_6位十六进制随机串.png
/// 例：2026-08-15_12-45-33_a82f31.png
/// 「时间戳 + 随机后缀」既保证肉眼可读的排序，又避免同一秒内重名。
/// </summary>
public static partial class ScreenshotFileName
{
    /// <summary>
    /// 本程序生成文件的命名正则。
    /// 缓存清理时只删除匹配该模式的文件——保证「只删本程序创建的截图，绝不碰用户其他文件」。
    /// </summary>
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_[0-9a-f]{6}\.png$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnFilePattern();

    /// <summary>生成一个随机的文件名（时间戳 + 6 位十六进制随机串）。</summary>
    public static string Generate(DateTime now)
    {
        string hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant();
        return $"{now:yyyy-MM-dd_HH-mm-ss}_{hex}.png";
    }

    /// <summary>
    /// 生成一个「在目标目录中不冲突」的文件名。
    /// 随机后缀理论上几乎不会冲突，但为稳妥起见，撞名时最多重试 10 次，
    /// 仍冲突则退化为时间戳 + 刻度值（唯一性兜底）。
    /// </summary>
    public static string GenerateUnique(string directory, DateTime now, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            string name = Generate(now);
            if (!File.Exists(Path.Combine(directory, name)))
            {
                return name;
            }
        }

        return $"{now:yyyy-MM-dd_HH-mm-ss}_{now.Ticks % 1_000_000:x6}.png";
    }

    /// <summary>判断文件名是否为「本程序创建的截图」（用于安全清理）。</summary>
    public static bool IsOwnFile(string fileName)
        => !string.IsNullOrWhiteSpace(fileName) && OwnFilePattern().IsMatch(fileName);
}
