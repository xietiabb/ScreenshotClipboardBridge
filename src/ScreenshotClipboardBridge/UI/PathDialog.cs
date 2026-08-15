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
    private const int DialogWidth = 560;
    private const int Margin = 16;

    private readonly string? _path;
    private readonly Action _openFolder;

    private readonly TextBox _pathBox;
    private readonly Button _copyBtn;
    private readonly Button _openBtn;
    private readonly Button _closeBtn;
    private readonly Label _hintLabel;

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
        AutoSize = false;
        ClientSize = new Size(DialogWidth, 152);

        int contentWidth = DialogWidth - Margin * 2; // 内容区可用宽度

        // ---- 第 1 行：提示文字 ----
        _hintLabel = new Label { AutoSize = true, Location = new Point(Margin, 14) };

        // ---- 第 2 行：路径文本框（只读、可全选手动复制）----
        _pathBox = new TextBox
        {
            ReadOnly = true,
            Location = new Point(Margin, 44),
            Width = contentWidth,
            Height = 26,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
        };

        // ---- 第 3 行：按钮（右下角，从右往左排列）----
        _copyBtn = new Button { Text = "复制路径", Width = 92, Height = 28 };
        _openBtn = new Button { Text = "打开文件夹", Width = 100, Height = 28 };
        _closeBtn = new Button { Text = "关闭", Width = 76, Height = 28 };
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Location = new Point(Margin, 100),
            Size = new Size(contentWidth, 34),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };
        buttons.Controls.Add(_closeBtn);
        buttons.Controls.Add(_openBtn);
        buttons.Controls.Add(_copyBtn);

        Controls.Add(_hintLabel);
        Controls.Add(_pathBox);
        Controls.Add(buttons);

        _copyBtn.Click += (_, _) => CopyPath();
        _openBtn.Click += (_, _) => _openFolder();
        _closeBtn.Click += (_, _) => Close();

        LoadPath();
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
