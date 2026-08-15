using System.Windows.Forms;

namespace ScreenshotClipboardBridge.UI;

/// <summary>
/// 「最近截图路径」对话框。
/// 用途：随时取回最近一次截图保存的文件路径。
/// 打开时若已有路径，自动复制到剪贴板 → 用户直接 Ctrl+V 粘贴即可。
/// 提供：复制路径 / 打开文件夹 / 关闭。
/// </summary>
public sealed class PathDialog : Form
{
    private readonly string? _path;
    private readonly Action _openFolder;

    private readonly TextBox _pathBox = new() { ReadOnly = true };
    private readonly Button _copyBtn = new() { Text = "复制路径" };
    private readonly Button _openBtn = new() { Text = "打开文件夹" };
    private readonly Button _closeBtn = new() { Text = "关闭" };
    private readonly Label _hintLabel = new() { AutoSize = true };

    public PathDialog(string? path, Action openFolder)
    {
        _path = path;
        _openFolder = openFolder;

        Text = "最近截图路径";
        Font = new Font("Microsoft YaHei UI", 9F);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        ClientSize = new Size(520, 0);

        BuildLayout();
        LoadPath();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            RowCount = 4,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(4),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // 第 1 行：提示
        _hintLabel.Text = _path is null ? "还没有转换过截图" : "已自动复制到剪贴板，可直接 Ctrl+V 粘贴 ✔";
        root.Controls.Add(_hintLabel, 0, 0);

        // 第 2 行：路径文本框（只读、可全选手动复制）
        _pathBox.Width = 480;
        _pathBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        root.Controls.Add(_pathBox, 0, 1);

        // 第 3 行：按钮
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        buttons.Controls.Add(_closeBtn);
        buttons.Controls.Add(_openBtn);
        buttons.Controls.Add(_copyBtn);
        root.Controls.Add(buttons, 0, 2);

        Controls.Add(root);

        _copyBtn.Click += (_, _) => CopyPath();
        _openBtn.Click += (_, _) => _openFolder();
        _closeBtn.Click += (_, _) => Close();
    }

    private void LoadPath()
    {
        if (_path is null)
        {
            _pathBox.Text = "（暂无记录）";
            _copyBtn.Enabled = false;
            _openBtn.Enabled = false;
            return;
        }

        _pathBox.Text = _path;
        _pathBox.SelectAll();
        _pathBox.Focus();
        CopyPath(); // 自动复制，满足「打开即粘贴」
    }

    /// <summary>把路径复制到剪贴板（失败时降级提示，不影响手动复制）。</summary>
    private void CopyPath()
    {
        if (_path is null)
        {
            return;
        }

        try
        {
            System.Windows.Forms.Clipboard.SetText(_path);
            _hintLabel.Text = "已复制到剪贴板，可直接 Ctrl+V 粘贴 ✔";
        }
        catch
        {
            _hintLabel.Text = "复制失败（剪贴板被占用），请手动选中上方路径复制";
        }
    }
}
