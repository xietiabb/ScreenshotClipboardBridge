using ScreenshotClipboardBridge.Core;
using ScreenshotClipboardBridge.Services;

namespace ScreenshotClipboardBridge.UI;

/// <summary>
/// 设置窗口（第一版，保持极简）：
///  - General：自动转换开关 / 开机启动开关 / 转换成功通知开关
///  - Storage：截图目录（路径 + 选择目录）/ 保存时间（下拉）/ 打开截图文件夹
/// 点「保存」把新配置写回（通过 Saved 事件交给 TrayContext 落盘并应用）。
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly CheckBox _enabledCheck = new() { Text = "自动转换截图", Checked = true };
    private readonly CheckBox _startupCheck = new() { Text = "开机自动启动", Checked = false };
    private readonly CheckBox _notificationCheck = new() { Text = "转换成功通知", Checked = true };
    private readonly TextBox _dirBox = new() { ReadOnly = false };
    private readonly Button _browseBtn = new() { Text = "选择目录...", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _openDirBtn = new() { Text = "打开截图文件夹", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly ComboBox _retentionBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _saveBtn = new() { Text = "保存", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, DialogResult = DialogResult.OK };
    private readonly Button _cancelBtn = new() { Text = "取消", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, DialogResult = DialogResult.Cancel };

    private readonly ScreenshotStore _store;

    /// <summary>用户点「保存」后产生的新配置；取消则为 null。</summary>
    public Config? Result { get; private set; }

    /// <summary>「保存」成功时触发（携带新配置，由 TrayContext 落盘并应用）。</summary>
    public event EventHandler<Config>? Saved;

    /// <summary>「打开截图文件夹」按钮点击事件（由 TrayContext 注入实现）。</summary>
    public event EventHandler? OpenFolderRequested;

    public SettingsForm(Config config, ScreenshotStore store)
    {
        _store = store;

        Text = "Screenshot Clipboard Bridge - 设置";
        Font = new Font("Microsoft YaHei UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = false;
        ClientSize = new Size(500, 330);

        BuildLayout();
        LoadConfig(config);
    }

    /// <summary>
    /// 组装控件布局：固定窗口尺寸 + 纯手动坐标。
    /// 不用 TableLayoutPanel/AutoSize 嵌套，按钮只保留 AutoSize（宽高随文字），
    /// 从根上避免「文字被挤压/截断」。
    /// </summary>
    private void BuildLayout()
    {
        const int margin = 12;
        int width = ClientSize.Width - margin * 2;

        // ---- General 分组 ----
        var general = new GroupBox { Text = "General", Bounds = new Rectangle(margin, margin, width, 112), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        _enabledCheck.Location = new Point(20, 30);
        _startupCheck.Location = new Point(20, 58);
        _notificationCheck.Location = new Point(20, 86);
        general.Controls.Add(_enabledCheck);
        general.Controls.Add(_startupCheck);
        general.Controls.Add(_notificationCheck);
        Controls.Add(general);

        // ---- Storage 分组 ----
        int storageY = margin + 112 + 8;
        var storage = new GroupBox { Text = "Storage", Bounds = new Rectangle(margin, storageY, width, 152), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        var dirLabel = new Label { Text = "截图目录:", AutoSize = true, Location = new Point(20, 31) };
        _dirBox.Bounds = new Rectangle(110, 27, 200, 25);
        _dirBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _browseBtn.Location = new Point(324, 25); // AutoSize，不设 Anchor，宽度随文字
        var retentionLabel = new Label { Text = "保存时间:", AutoSize = true, Location = new Point(20, 71) };
        _retentionBox.Bounds = new Rectangle(110, 67, 160, 25);
        _openDirBtn.Location = new Point(110, 107); // AutoSize
        storage.Controls.Add(dirLabel);
        storage.Controls.Add(_dirBox);
        storage.Controls.Add(_browseBtn);
        storage.Controls.Add(retentionLabel);
        storage.Controls.Add(_retentionBox);
        storage.Controls.Add(_openDirBtn);
        Controls.Add(storage);

        // ---- 底部按钮（右下角，FlowLayoutPanel 动态布局 AutoSize 按钮）----
        var bottom = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Location = new Point(margin, storageY + 152 + 10),
            Size = new Size(width, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };
        bottom.Controls.Add(_saveBtn);
        bottom.Controls.Add(_cancelBtn);
        Controls.Add(bottom);

        // 事件
        _browseBtn.Click += OnBrowseClick;
        _openDirBtn.Click += (_, _) => OpenFolderRequested?.Invoke(this, EventArgs.Empty);
        _saveBtn.Click += OnSaveClick;
        _cancelBtn.Click += (_, _) => Close();

        // 保存时间下拉选项
        _retentionBox.Items.Add(new RetentionItem(1, "1 天"));
        _retentionBox.Items.Add(new RetentionItem(3, "3 天"));
        _retentionBox.Items.Add(new RetentionItem(7, "7 天"));
        _retentionBox.Items.Add(new RetentionItem(30, "30 天"));
        _retentionBox.Items.Add(new RetentionItem(0, "永久保存"));
    }

    /// <summary>把当前配置填充到控件。</summary>
    private void LoadConfig(Config config)
    {
        _enabledCheck.Checked = config.Enabled;
        _startupCheck.Checked = config.Startup;
        _notificationCheck.Checked = config.Notification;
        _dirBox.Text = App.AppPaths.ResolveSaveDirectory(config.SaveDirectory);

        RetentionItem? selected = _retentionBox.Items
            .Cast<RetentionItem>()
            .FirstOrDefault(i => i.Days == config.RetentionDays);
        _retentionBox.SelectedItem = selected ?? (RetentionItem)_retentionBox.Items[2]!; // 默认 7 天
    }

    /// <summary>选择目录：弹 FolderBrowserDialog 并回填路径。</summary>
    private void OnBrowseClick(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择截图保存目录",
            SelectedPath = Directory.Exists(_dirBox.Text) ? _dirBox.Text : App.AppPaths.DefaultImageDir,
            ShowNewFolderButton = true,
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _dirBox.Text = dialog.SelectedPath;
        }
    }

    /// <summary>校验并产出新配置。</summary>
    private void OnSaveClick(object? sender, EventArgs e)
    {
        string dir = _dirBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(dir))
        {
            MessageBox.Show(this, "请选择截图保存目录。", "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 目录不在当前已解析路径下时才尝试创建（避免无权限路径导致保存失败）。
        var retention = _retentionBox.SelectedItem as RetentionItem ?? (RetentionItem)_retentionBox.Items[2]!;

        Result = new Config
        {
            Enabled = _enabledCheck.Checked,
            Startup = _startupCheck.Checked,
            Notification = _notificationCheck.Checked,
            SaveDirectory = string.Equals(dir, App.AppPaths.DefaultImageDir, StringComparison.OrdinalIgnoreCase)
                ? "default"
                : dir,
            RetentionDays = retention.Days,
        };

        Saved?.Invoke(this, Result);
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>下拉框项：保留天数 + 显示文本。</summary>
    private sealed record RetentionItem(int Days, string Label)
    {
        public override string ToString() => Label;
    }
}
