using System.Text.Json;
using System.Text.Json.Serialization;
using ScreenshotClipboardBridge.App;

namespace ScreenshotClipboardBridge.Services;

/// <summary>
/// 配置持久化服务：JSON 文件读写（%LOCALAPPDATA%\ScreenshotClipboardBridge\config.json）。
/// 任何读取/反序列化异常都回退到默认配置，保证程序永不因配置损坏而崩溃。
/// </summary>
public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // 输出 { "enabled": true, ... } 风格
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public ConfigService(string? path = null) => _path = path ?? AppPaths.ConfigPath;

    /// <summary>加载配置；文件不存在或损坏时返回默认配置。</summary>
    public Config Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new Config();
            }

            string json = File.ReadAllText(_path);
            Config? config = JsonSerializer.Deserialize<Config>(json, JsonOptions);
            return Sanitize(config ?? new Config());
        }
        catch
        {
            return new Config();
        }
    }

    /// <summary>保存配置（先清理非法值再落盘）。</summary>
    public void Save(Config config)
    {
        Config clean = Sanitize(config);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(clean, JsonOptions));
    }

    /// <summary>
    /// 校验并规整配置值：
    ///  - RetentionDays 只允许 0(永久)/1/3/7/30，其余一律回退 7；
    ///  - SaveDirectory 空白视为 "default"。
    /// </summary>
    private static Config Sanitize(Config config)
    {
        config.RetentionDays = config.RetentionDays switch
        {
            0 or 1 or 3 or 7 or 30 => config.RetentionDays,
            _ => 7,
        };

        if (string.IsNullOrWhiteSpace(config.SaveDirectory))
        {
            config.SaveDirectory = "default";
        }

        return config;
    }
}
