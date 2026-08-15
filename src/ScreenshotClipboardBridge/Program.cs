using System.Windows.Forms;
using ScreenshotClipboardBridge.App;
using ScreenshotClipboardBridge.Clipboard;
using ScreenshotClipboardBridge.Core;
using ScreenshotClipboardBridge.Services;
using ScreenshotClipboardBridge.UI;

namespace ScreenshotClipboardBridge;

/// <summary>
/// 程序入口。
/// 流程：单实例检查 → 加载配置 → 组装核心管线（剪贴板监听/图片处理/保存/防循环）→ 启动托盘 → 进入消息循环。
/// 全程无主窗口，仅系统托盘 + 设置窗口。
/// </summary>
internal static class Program
{
    /// <summary>单实例互斥体名称（Local 作用域，只在本用户会话内互斥）。</summary>
    private const string MutexName = @"Local\ScreenshotClipboardBridge_SingleInstance";

    /// <summary>
    /// 主入口。必须标记 STAThread：Windows 剪贴板（Clipboard）与 WinForms 均要求 STA 线程。
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        // WinForms 应用初始化（视觉样式、默认字体、DPI 等，.NET 6+ 自动生成）。
        ApplicationConfiguration.Initialize();
        AppLog.Write("startup", $"进程启动 pid={Environment.ProcessId}");

        // 布局预览调试模式（开发用，不影响正常启动）：
        //   --layout-preview [输出目录]
        // 启动后自动渲染「设置窗口 + 最近截图路径对话框」并保存 PNG 截图，然后退出。
        if (args.Contains("--layout-preview"))
        {
            RunLayoutPreview(args.Length > 1 ? args[1] : Path.Combine(AppPaths.RootDir, "layout-preview"));
            return;
        }

        // ---- 单实例保护：已运行时提示并退出，避免两个实例同时抢剪贴板 ----
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Screenshot Clipboard Bridge 已经在运行中。\n请查看系统托盘。",
                "Screenshot Clipboard Bridge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // ---- 配置 ----
        var configService = new ConfigService();
        var config = configService.Load();
        // 开机自启以注册表为准（防止用户手动删注册表后配置残留）。
        config.Startup = StartupService.IsEnabled();

        // ---- 核心管线组装 ----
        var store = new ScreenshotStore(() => config.SaveDirectory);                       // 截图存储（目录可配置）
        var loopGuard = new LoopGuard();                                                  // 防死循环
        var handler = new ClipboardImageHandler(store, loopGuard, path => System.Windows.Forms.Clipboard.SetText(path)); // 图片 → 保存 → 路径写回
        var retention = new RetentionService(store, () => config.RetentionDays);           // 定期清理过期截图

        // 「最近截图」持久化：程序重启后仍能记住上次截图（对话框/未来 MCP 依赖）。
        var recentStore = new RecentScreenshotStore();
        RecentScreenshotStore.Entry? recent = recentStore.Load();
        if (recent is not null)
        {
            handler.RestoreLastSaved(recent.Path, recent.CreatedAtUtc);
            AppLog.Write("startup", $"已恢复最近截图记录: {recent.Path}");
        }

        // ---- 托盘常驻 ----
        // 剪贴板监听（AddClipboardFormatListener 原生事件，非轮询）。
        using var monitor = new ClipboardMonitor();
        bool listenerStarted = monitor.Start();
        AppLog.Write("startup", $"监听器注册: listenerStarted={listenerStarted}, 配置: enabled={config.Enabled}, notification={config.Notification}, retention={config.RetentionDays}");
        using var trayContext = new TrayContext(config, configService, store, handler, retention, recentStore, listenerStarted);
        monitor.ClipboardChanged += trayContext.OnClipboardChanged;

        // 主消息循环（ApplicationContext 模式：无主窗体，靠托盘图标存活）。
        Application.Run(trayContext);
        AppLog.Write("exit", "程序退出");
    }

    /// <summary>
    /// 布局预览：渲染设置窗口与最近截图路径对话框，保存为 PNG（开发调试用）。
    /// </summary>
    private static void RunLayoutPreview(string outputDir)
    {
        try
        {
            Directory.CreateDirectory(outputDir);
            var config = new Config();
            var store = new Core.ScreenshotStore(() => AppPaths.DefaultImageDir);

            var settings = new UI.SettingsForm(config, store);
            settings.Show();
            var pathDialog = new UI.PathDialog(
                @"C:\Users\tian51\AppData\Local\ScreenshotClipboardBridge\images\2026-08-15_15-20-28_48eb44.png",
                () => { });
            pathDialog.Show();

            // 让窗口完成一次真实布局渲染
            Application.DoEvents();
            Thread.Sleep(800);
            Application.DoEvents();

            // 输出控件树诊断信息（Bounds / AutoSize / PreferredSize / Font）
            DumpControls(settings, "设置窗口");
            DumpControls(pathDialog, "最近路径对话框");

            SaveWindowImage(settings, Path.Combine(outputDir, "layout-settings.png"));
            SaveWindowImage(pathDialog, Path.Combine(outputDir, "layout-pathdialog.png"));

            AppLog.Write("preview", $"布局预览已保存到 {outputDir}");
            settings.Close();
            pathDialog.Close();
        }
        catch (Exception ex)
        {
            AppLog.Write("preview", $"布局预览失败: {ex}");
        }
    }

    /// <summary>把窗口客户区渲染为 PNG。</summary>
    private static void SaveWindowImage(System.Windows.Forms.Form form, string path)
    {
        using var bitmap = new System.Drawing.Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>递归输出控件树诊断（布局问题定位用）。</summary>
    private static void DumpControls(System.Windows.Forms.Form root, string title)
    {
        void Walk(System.Windows.Forms.Control parent, string indent)
        {
            foreach (System.Windows.Forms.Control c in parent.Controls)
            {
                AppLog.Write("preview",
                    $"{title} | {indent}{c.GetType().Name} Text='{c.Text}' Bounds={c.Bounds} " +
                    $"AutoSize={c.AutoSize} PreferredSize={c.PreferredSize} Font={c.Font.Name}/{c.Font.Size} DPI={c.DeviceDpi}");
                Walk(c, indent + "  ");
            }
        }

        AppLog.Write("preview", $"{title} | {root.GetType().Name} Bounds={root.Bounds} ClientSize={root.ClientSize} AutoScaleMode={root.AutoScaleMode} DPI={root.DeviceDpi}");
        Walk(root, "  ");
    }
}
