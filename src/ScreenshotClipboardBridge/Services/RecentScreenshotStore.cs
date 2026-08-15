using System.Text.Json;
using ScreenshotClipboardBridge.App;

namespace ScreenshotClipboardBridge.Services;

/// <summary>
/// 「最近一次截图」的持久化存储（%LOCALAPPDATA%\ScreenshotClipboardBridge\last-screenshot.json）。
/// 目的：程序重启后仍能记住最近截图路径——「最近截图路径」对话框与未来 MCP 扩展都依赖它。
/// 任何读写异常都静默降级（持久化只是辅助，绝不能影响主流程）。
/// </summary>
public sealed class RecentScreenshotStore
{
    /// <summary>持久化条目：文件路径 + 创建时间（UTC）。</summary>
    public sealed record Entry(string Path, DateTime CreatedAtUtc);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _filePath;

    public RecentScreenshotStore(string? filePath = null)
        => _filePath = filePath ?? Path.Combine(AppPaths.RootDir, "last-screenshot.json");

    /// <summary>保存最近截图记录（覆盖式，只保留最新一条）。</summary>
    public void Save(Entry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(entry, JsonOptions));
        }
        catch
        {
            // 忽略：记录丢失不影响核心功能
        }
    }

    /// <summary>加载最近截图记录；文件缺失或损坏返回 null。</summary>
    public Entry? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<Entry>(File.ReadAllText(_filePath), JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
