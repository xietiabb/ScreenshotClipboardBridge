using System.Diagnostics;
using ScreenshotClipboardBridge.App;

namespace ScreenshotClipboardBridge.Core;

/// <summary>
/// 截图存储：负责保存 PNG、安全清理、打开目录。
///
/// 安全原则：清理（Cleanup / ClearAll）只会删除「文件名匹配本程序命名模式」的 PNG，
/// 且只在当前配置的截图目录内操作——绝不会触碰用户的其他文件。
/// </summary>
public sealed class ScreenshotStore
{
    private readonly Func<string> _getSaveDirectory;

    /// <summary>
    /// 构造。保存目录是可变的（用户在设置里改），因此注入一个读取当前配置的委托。
    /// </summary>
    public ScreenshotStore(Func<string> getSaveDirectory)
        => _getSaveDirectory = getSaveDirectory ?? throw new ArgumentNullException(nameof(getSaveDirectory));

    /// <summary>当前生效的截图保存目录（已解析配置中的 "default"）。</summary>
    public string DirectoryPath => AppPaths.ResolveSaveDirectory(_getSaveDirectory());

    /// <summary>
    /// 把 PNG 字节保存为新文件，返回绝对路径。
    /// </summary>
    public string Save(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        string dir = DirectoryPath;
        Directory.CreateDirectory(dir);

        string name = ScreenshotFileName.GenerateUnique(dir, DateTime.Now);
        string path = Path.Combine(dir, name);

        // 先写临时文件再改名，避免写入中途崩溃产生半截文件。
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, pngBytes);
        File.Move(temp, path, overwrite: false);

        return path;
    }

    /// <summary>
    /// 按保留天数清理过期截图。
    /// </summary>
    /// <param name="retentionDays">保留天数；&lt;=0 表示永久保存，不清理。</param>
    /// <returns>删除的文件数。</returns>
    public int Cleanup(int retentionDays)
    {
        if (retentionDays <= 0 || !Directory.Exists(DirectoryPath))
        {
            return 0;
        }

        DateTime cutoff = DateTime.Now - TimeSpan.FromDays(retentionDays);
        int deleted = 0;

        foreach (string file in Directory.EnumerateFiles(DirectoryPath, "*.png"))
        {
            // 只清理本程序命名的文件。
            if (!ScreenshotFileName.IsOwnFile(Path.GetFileName(file)))
            {
                continue;
            }

            try
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    File.Delete(file);
                    deleted++;
                }
            }
            catch
            {
                // 文件可能正被其他程序占用，跳过即可，不影响其余清理。
            }
        }

        return deleted;
    }

    /// <summary>
    /// 立即清空本程序保存的所有截图（托盘「清理缓存」）。
    /// </summary>
    public int ClearAll()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return 0;
        }

        int deleted = 0;
        foreach (string file in Directory.EnumerateFiles(DirectoryPath, "*.png"))
        {
            if (!ScreenshotFileName.IsOwnFile(Path.GetFileName(file)))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch
            {
                // 同上：跳过占用中的文件
            }
        }

        return deleted;
    }

    /// <summary>
    /// 在资源管理器中打开截图保存目录（不存在则先创建）。
    /// </summary>
    public void OpenInExplorer()
    {
        Directory.CreateDirectory(DirectoryPath);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{DirectoryPath}\"") { UseShellExecute = true });
    }
}
