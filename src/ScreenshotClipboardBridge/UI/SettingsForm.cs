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
    // 注意：CheckBox 在 .NET 8 里默认 AutoSize=False（宽度固定 104px），
    // 高 DPI（125%/150%/175%）下文字会被截断，必须显式 AutoSize=true 让宽度随文字。
    private readonly CheckBox _enabledCheck = new() { Text = "自动转换截图", Checked = true, AutoSize = true };
    private readonly CheckBox _startupCheck = new() { Text = "开机自动启动", Checked = false, AutoSize = true };
    private readonly CheckBox _notificationCheck = new() { Text = "转换成功通知", Checked = true, AutoSize = true };
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
        ClientSize = new Size(600, 430);

        BuildLayout();
        LoadConfig(config);
    }

    /// <summary>
    /// 组装控件布局：全部使用「自适应尺寸」容器（AutoSize + FlowLayout/TableLayout）。
    /// 容器按控件的实际 PreferredSize（已按当前 DPI 测量）自动决定尺寸，
    /// 任何 DPI/字体缩放下都不会出现文字截断、控件重叠或边框被盖。
    /// </summary>
    private void BuildLayout()
    {
        const int margin = 16;

        // ---- General：三个复选框纵向自动排列 ----
        var general = new GroupBox
        {
            Text = "General",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = new Point(margin, margin),
            Padding = new Padding(12, 10, 12, 12),
        };
        var generalFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        _enabledCheck.Margin = new Padding(0, 6, 0, 6);
        _startupCheck.Margin = new Padding(0, 6, 0, 6);
        _notificationCheck.Margin = new Padding(0, 6, 0, 6);
        generalFlow.Controls.Add(_enabledCheck);
        generalFlow.Controls.Add(_startupCheck);
        generalFlow.Controls.Add(_notificationCheck);
        general.Controls.Add(generalFlow);
        Controls.Add(general);

        // ---- Storage：两行表格式自动布局 ----
        int storageY = general.Bottom + 12;
        var storage = new GroupBox
        {
            Text = "Storage",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Location = new Point(margin, storageY),
            Padding = new Padding(12, 10, 12, 12),
        };
        var storageLayout = new TableLayoutPanel
        {
            ColumnCount = 3,
            RowCount = 3,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        storageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        storageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        storageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        storageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var dirLabel = new Label { Text = "截图目录:", AutoSize = true, Margin = new Padding(0, 4, 10, 0) };
        _dirBox.Dock = DockStyle.Fill;
        _dirBox.Margin = new Padding(0, 2, 8, 10);
        _browseBtn.Margin = new Padding(0, 0, 0, 10);
        storageLayout.Controls.Add(dirLabel, 0, 0);
        storageLayout.Controls.Add(_dirBox, 1, 0);
        storageLayout.Controls.Add(_browseBtn, 2, 0);

        var retentionLabel = new Label { Text = "保存时间:", AutoSize = true, Margin = new Padding(0, 4, 10, 0) };
        _retentionBox.Dock = DockStyle.Fill;
        _retentionBox.Margin = new Padding(0, 2, 8, 10);
        storageLayout.Controls.Add(retentionLabel, 0, 1);
        storageLayout.Controls.Add(_retentionBox, 1, 1);

        _openDirBtn.Margin = new Padding(0, 0, 0, 0);
        storageLayout.Controls.Add(_openDirBtn, 1, 2);
        storageLayout.SetColumnSpan(_openDirBtn, 2);
        storage.Controls.Add(storageLayout);
        Controls.Add(storage);

        // ---- 底部按钮（右下角，FlowLayoutPanel 动态布局 AutoSize 按钮）----
        var bottom = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Location = new Point(margin, storage.Bottom + 14),
            Width = ClientSize.Width - margin * 2,
            Height = 34,
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
        // 路径较长时默认显示末尾（文件名部分），方便查看。
        _dirBox.SelectionStart = _dirBox.Text.Length;
        _dirBox.ScrollToCaret();

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
