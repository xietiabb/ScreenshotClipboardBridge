using System.Windows.Forms;
using ScreenshotClipboardBridge.Clipboard;
using ScreenshotClipboardBridge.Core;
using ScreenshotClipboardBridge.Services;

namespace ScreenshotClipboardBridge.UI;

/// <summary>
/// 托盘常驻上下文（ApplicationContext）：程序没有主窗口，生命周期由它承载。
/// 职责：
///  1. 系统托盘图标 + 右键菜单（启用/暂停/打开目录/设置/开机自启/清理缓存/退出）；
///  2. 剪贴板变化事件 → 防抖（300ms）→ 调用 ClipboardImageHandler 处理；
///  3. 设置窗口的单例管理；
///  4. 通知（转换成功 Toast / 清理结果）。
/// </summary>
public sealed class TrayContext : ApplicationContext
{
    /// <summary>防抖窗口：截图工具可能在极短时间内多次更新剪贴板（PNG/DIB 多种格式），
    /// 统一折叠为一次处理，绝不重复保存。</summary>
    private const int DebounceMilliseconds = 300;

    private readonly Config _config;
    private readonly ConfigService _configService;
    private readonly ClipboardImageHandler _handler;
    private readonly ScreenshotStore _store;
    private readonly RetentionService _retention;
    private readonly ToastService _toast;

    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _debounceTimer;
    private readonly System.Windows.Forms.Timer? _pollTimer; // 仅当原生监听器注册失败时启用
    private readonly ToolStripMenuItem _enableItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _startupItem;

    private SettingsForm? _settingsForm;
    private PathDialog? _pathDialog;
    private System.Windows.Forms.Timer? _clickTimer; // 区分单击/双击的延迟判定
    private bool _processing;

    public TrayContext(
        Config config,
        ConfigService configService,
        ScreenshotStore store,
        ClipboardImageHandler handler,
        RetentionService retention,
        bool clipboardListenerActive)
    {
        _config = config;
        _configService = configService;
        _store = store;
        _handler = handler;
        _retention = retention;

        // 托盘图标（程序唯一常驻的可见元素）
        _tray = new NotifyIcon
        {
            Icon = TrayIcons.Create(),
            Text = "Screenshot Clipboard Bridge",
            Visible = true,
        };
        _toast = new ToastService(_tray);

        // 右键菜单
        var menu = new ContextMenuStrip();
        _enableItem = new ToolStripMenuItem("启用自动转换");
        _pauseItem = new ToolStripMenuItem("暂停自动转换");
        _startupItem = new ToolStripMenuItem("开机自动启动");

        // 置顶项：随时取回最近截图路径（对话框打开即自动复制，可直接 Ctrl+V）
        menu.Items.Add("📋 最近截图路径…", null, (_, _) => ShowPathDialog());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_enableItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("打开截图保存目录", null, (_, _) => _store.OpenInExplorer());
        menu.Items.Add("设置", null, (_, _) => OpenSettings());
        menu.Items.Add(_startupItem);
        menu.Items.Add("清理缓存", null, (_, _) => CleanupCache());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());

        _tray.ContextMenuStrip = menu;

        // 左键单击 → 最近路径对话框；左键双击 → 设置。
        // 单击比双击先触发，用系统双击时间作为延迟，双击时取消单击动作。
        _clickTimer = new System.Windows.Forms.Timer { Interval = SystemInformation.DoubleClickTime + 50 };
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            ShowPathDialog();
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _clickTimer.Stop();
            _clickTimer.Start();
        };
        _tray.DoubleClick += (_, _) =>
        {
            _clickTimer.Stop();
            OpenSettings();
        };

        _enableItem.Click += (_, _) => SetEnabled(true);
        _pauseItem.Click += (_, _) => SetEnabled(false);
        _startupItem.Click += (_, _) => ToggleStartup();

        // 防抖定时器（WinForms Timer，跑在 UI 线程，避免跨线程剪贴板访问）
        _debounceTimer = new System.Windows.Forms.Timer { Interval = DebounceMilliseconds };
        _debounceTimer.Tick += (_, _) => ProcessPending();

        // 极端情况兜底：原生监听器注册失败时，退化为 500ms 轻量轮询
        // （ProcessPending 对非图片内容天然空转，不会误处理）。
        if (!clipboardListenerActive)
        {
            _pollTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _pollTimer.Tick += (_, _) => ProcessPending();
            _pollTimer.Start();
        }

        UpdateMenuState();
    }

    /// <summary>
    /// 剪贴板变化入口（由 ClipboardMonitor 在 UI 线程回调）。
    /// 只做「重启防抖定时器」，真正的读取放在定时器回调里。
    /// </summary>
    public void OnClipboardChanged(object? sender, EventArgs e)
    {
        if (!_config.Enabled)
        {
            return;
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    /// <summary>防抖到期：读取剪贴板并尝试转换。</summary>
    private void ProcessPending()
    {
        _debounceTimer.Stop();

        // 上轮仍在处理（例如剪贴板被占用重试中）：重排一次，保证不丢事件。
        if (_processing)
        {
            _debounceTimer.Start();
            return;
        }

        _processing = true;
        try
        {
            if (!_config.Enabled)
            {
                return;
            }

            IClipboardSource source = WinClipboardSource.Capture();
            bool converted = _handler.TryConvert(source);
            string? savedPath = _handler.LastSavedPath; // 转换成功后才更新，需在 TryConvert 之后读取
            App.AppLog.Write("process", $"尝试转换: converted={converted}, image={source.HasImage}, fileDrop={source.HasFileDrop}, text={source.HasText}, 路径={savedPath ?? "无"}");
            if (converted && savedPath is not null)
            {
                // 转换成功：更新托盘提示 + 可选通知
                string fileName = Path.GetFileName(savedPath);
                _tray.Text = $"Screenshot Clipboard Bridge — {fileName}";
                if (_config.Notification)
                {
                    _toast.ShowSaved(fileName);
                }
            }
        }
        catch (Exception ex)
        {
            // 任何异常都绝不能拖垮托盘进程：静默忽略，等待下次事件。
            App.AppLog.Write("error", $"处理剪贴板时异常: {ex}");
        }
        finally
        {
            _processing = false;
        }
    }

    /// <summary>启用/暂停自动转换。</summary>
    private void SetEnabled(bool enabled)
    {
        _config.Enabled = enabled;
        _configService.Save(_config);
        UpdateMenuState();
    }

    /// <summary>切换开机自启（写注册表 + 保存配置）。</summary>
    private void ToggleStartup()
    {
        _config.Startup = !_config.Startup;
        StartupService.SetEnabled(_config.Startup);
        _configService.Save(_config);
        UpdateMenuState();
    }

    /// <summary>清理缓存：确认后删除本程序创建的全部截图。</summary>
    private void CleanupCache()
    {
        var confirm = MessageBox.Show(
            "确定要删除本程序保存的全部截图吗？\n此操作只删除本程序创建的截图文件。",
            "清理缓存",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        int deleted = _store.ClearAll();
        _tray.ShowBalloonTip(2000, "清理缓存", $"已删除 {deleted} 个截图。", ToolTipIcon.Info);
    }

    /// <summary>打开「最近截图路径」对话框（单例：已打开则激活）。</summary>
    private void ShowPathDialog()
    {
        if (_pathDialog is not null && !_pathDialog.IsDisposed)
        {
            _pathDialog.Activate();
            return;
        }

        var dialog = new PathDialog(_handler.LastSavedPath, () => _store.OpenInExplorer());
        dialog.FormClosed += (_, _) => _pathDialog = null;
        _pathDialog = dialog;
        dialog.Show();
    }

    /// <summary>打开设置窗口（单例：已打开则激活）。</summary>
    private void OpenSettings()
    {
        if (_settingsForm is not null && !_settingsForm.IsDisposed)
        {
            _settingsForm.Activate();
            return;
        }

        var form = new SettingsForm(_config, _store);
        form.OpenFolderRequested += (_, _) => _store.OpenInExplorer();
        form.FormClosed += (_, _) => _settingsForm = null;
        form.Saved += OnSettingsSaved;
        _settingsForm = form;
        form.Show();
    }

    /// <summary>设置保存回调：应用新配置并立即生效。</summary>
    private void OnSettingsSaved(object? sender, Config newConfig)
    {
        _config.Enabled = newConfig.Enabled;
        _config.SaveDirectory = newConfig.SaveDirectory;
        _config.RetentionDays = newConfig.RetentionDays;
        _config.Notification = newConfig.Notification;
        _config.Startup = newConfig.Startup;

        // 开机自启以设置页为准，同步注册表。
        StartupService.SetEnabled(_config.Startup);

        _configService.Save(_config);
        UpdateMenuState();

        // 保留天数可能变小，立即执行一次清理。
        _retention.RunOnce();
    }

    /// <summary>同步菜单勾选/可用状态。</summary>
    private void UpdateMenuState()
    {
        _enableItem.Enabled = !_config.Enabled;
        _pauseItem.Enabled = _config.Enabled;
        _startupItem.Checked = _config.Startup;
    }

    /// <summary>退出程序：隐藏并释放托盘图标，结束消息循环。</summary>
    private void ExitApplication()
    {
        _tray.Visible = false;
        _tray.Icon?.Dispose();
        _tray.Dispose();
        Application.Exit();
    }

    /// <summary>释放资源（ApplicationContext.Dispose 的扩展点）。</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _debounceTimer.Dispose();
            _pollTimer?.Dispose();
            _clickTimer?.Dispose();
            _settingsForm?.Dispose();
            _pathDialog?.Dispose();
        }

        base.Dispose(disposing);
    }
}
