using System.Windows.Forms;

namespace ScreenshotClipboardBridge.UI;

/// <summary>
/// 「最近截图路径」对话框。
/// 用途：随时取回最近一次截图保存的文件路径。
/// 打开时若已有路径，自动复制到剪贴板 → 用户直接 Ctrl+V 粘贴即可。
/// 提供：复制路径 / 打开文件夹 / 关闭。
///
/// 布局说明：固定尺寸 + 手动坐标（不叠加 AutoSize），
/// 避免 WinForms 中 AutoSize 与 ClientSize 混用导致的控件被遮挡问题。
/// </summary>
public sealed class PathDialog : Form
{
    private const int DialogWidth = 660;
    private const int Margin = 20;

    private readonly string? _path;
    private readonly Action _openFolder;

    private readonly TextBox _pathBox;
    private readonly Button _copyBtn;
    private readonly Button _openBtn;
    private readonly Button _closeBtn;
    private readonly Label _hintLabel;
    private readonly ToolTip _tooltip = new();

    public PathDialog(string? path, Action openFolder)
    {
        _path = path;
        _openFolder = openFolder;

        Text = "最近截图路径";
        Font = new Font("Microsoft YaHei UI", 9F);
        Icon = TrayIcons.Shared; // 窗口标题栏/任务栏窗口图标与应用图标一致
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = false;
        ClientSize = new Size(DialogWidth, 186);

        int contentWidth = DialogWidth - Margin * 2; // 内容区可用宽度

        // ---- 第 1 行：提示文字 ----
        _hintLabel = new Label { AutoSize = true, Location = new Point(Margin, 18) };

        // ---- 第 2 行：路径文本框（只读、可全选手动复制）----
        _pathBox = new TextBox
        {
            ReadOnly = true,
            Location = new Point(Margin, 52),
            Width = contentWidth,
            Height = 30,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
        };

        // ---- 第 3 行：按钮（右下角，从右往左排列）
        // 按钮只保留 AutoSize：宽高完全由文字决定，任何 DPI/字体下都不会挤压截断。
        _copyBtn = new Button { Text = "复制路径", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        _openBtn = new Button { Text = "打开文件夹", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        _closeBtn = new Button { Text = "关闭", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Location = new Point(Margin, 120),
            Size = new Size(contentWidth, 40),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };
        buttons.Controls.Add(_closeBtn);
        buttons.Controls.Add(_openBtn);
        buttons.Controls.Add(_copyBtn);

        Controls.Add(_hintLabel);
        Controls.Add(_pathBox);
        Controls.Add(buttons);

        // 路径较长显示不全时，鼠标悬停查看完整路径。
        _pathBox.TextChanged += (_, _) => _tooltip.SetToolTip(_pathBox, _pathBox.Text);

        _copyBtn.Click += (_, _) => CopyPath();
        _openBtn.Click += (_, _) => _openFolder();
        _closeBtn.Click += (_, _) => Close();

        LoadPath();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tooltip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void LoadPath()
    {
        if (_path is null)
        {
            _hintLabel.Text = "还没有转换过截图";
            _pathBox.Text = "（暂无记录）";
            _copyBtn.Enabled = false;
            _openBtn.Enabled = false;
            return;
        }

        _hintLabel.Text = "已自动复制到剪贴板，可直接 Ctrl+V 粘贴 ✔";
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
