using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ScreenshotClipboardBridge.UI;

/// <summary>
/// 托盘图标工厂：运行时用 GDI+ 绘制一个简单的「截图 + 剪贴板」图标，
/// 避免为第一版引入 .ico 资源文件。
/// 设计：蓝色圆角方块底 + 白色图片框 + 山与太阳（示意截图）。
/// </summary>
internal static class TrayIcons
{
    /// <summary>创建 16x16（托盘尺寸）图标。进程生命周期内持有即可，无需频繁重建。</summary>
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // 圆角蓝色底板
            using var bg = new SolidBrush(Color.FromArgb(0, 120, 215));
            using var path = RoundedRect(new Rectangle(2, 2, 28, 28), 7);
            g.FillPath(bg, path);

            // 白色「图片」外框
            using var framePen = new Pen(Color.White, 2.4f);
            g.DrawRectangle(framePen, 7, 8, 18, 14);

            // 山（示意截图内容）
            using var mountainPen = new Pen(Color.White, 2f);
            g.DrawLine(mountainPen, 10, 20, 15, 13);
            g.DrawLine(mountainPen, 15, 13, 20, 20);

            // 太阳
            using var sunBrush = new SolidBrush(Color.White);
            g.FillEllipse(sunBrush, 19, 10, 3.4f, 3.4f);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>绘制圆角矩形路径。</summary>
    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
