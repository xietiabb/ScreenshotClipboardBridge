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
    private readonly Button _browseBtn = new() { Text = "选择目录..." };
    private readonly Button _openDirBtn = new() { Text = "打开截图文件夹" };
    private readonly ComboBox _retentionBox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _saveBtn = new() { Text = "保存", DialogResult = DialogResult.OK };
    private readonly Button _cancelBtn = new() { Text = "取消", DialogResult = DialogResult.Cancel };

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
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        BuildLayout();
        LoadConfig(config);
    }

    /// <summary>组装控件布局（TableLayoutPanel，两列）。</summary>
    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        // ---- General 分组 ----
        var general = new GroupBox { Text = "General", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var generalLayout = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(6),
        };
        generalLayout.Controls.Add(_enabledCheck);
        generalLayout.Controls.Add(_startupCheck);
        generalLayout.Controls.Add(_notificationCheck);
        general.Controls.Add(generalLayout);

        // ---- Storage 分组 ----
        var storage = new GroupBox { Text = "Storage", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var storageLayout = new TableLayoutPanel
        {
            ColumnCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Padding = new Padding(6),
        };
        storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        storageLayout.Controls.Add(new Label { Text = "截图目录:", AutoSize = true }, 0, 0);
        storageLayout.Controls.Add(_dirBox, 1, 0);
        storageLayout.Controls.Add(_browseBtn, 2, 0);

        storageLayout.Controls.Add(new Label { Text = "保存时间:", AutoSize = true }, 0, 1);
        storageLayout.Controls.Add(_retentionBox, 1, 1);

        storageLayout.Controls.Add(_openDirBtn, 1, 2);
        storageLayout.SetColumnSpan(_openDirBtn, 2);

        storage.Controls.Add(storageLayout);

        // ---- 按钮行 ----
        var buttonRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        buttonRow.Controls.Add(_saveBtn);
        buttonRow.Controls.Add(_cancelBtn);

        // ---- 根布局 ----
        root.RowCount = 3;
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(general, 0, 0);
        root.Controls.Add(storage, 0, 1);
        root.Controls.Add(buttonRow, 0, 2);

        Controls.Add(root);

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
