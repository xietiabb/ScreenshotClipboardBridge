# =====================================================================
# PNG -> 多尺寸 ICO 转换脚本（带「去白边」）
#
# 处理流程：
#   1. 加载原图（24bpp/32bpp 均可）
#   2. 去白边：从图像四边向内做连通域填充，把「与边缘相连的白色区域」
#      设为透明——只去掉背景白边，保留图标内部的白色元素（太阳、横线等）
#   3. 缩放到 16/24/32/48/64/128/256 七档尺寸（内嵌 PNG 数据，Vista+ 支持）
#   4. 打包为 assets/app.ico
#
# 用途：程序 EXE 图标（csproj ApplicationIcon）+ 系统托盘图标（嵌入资源）。
# 用法：powershell -File scripts\make-icon.ps1 -PngPath "C:\path\to\icon.png"
# =====================================================================
param(
    [Parameter(Mandatory = $true)][string]$PngPath,
    [string]$OutIco = "src\ScreenshotClipboardBridge\assets\app.ico",
    [int]$WhiteThreshold = 238   # 视为白色的 RGB 下限（238=较保守，只去纯白）
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# ---- 内联 C#：边缘连通去白（比 PowerShell 循环快几个数量级）----
Add-Type -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public static class IconHelper
{
    /// <summary>
    /// 去掉与图像边缘相连的白色区域（BFS 连通域填充），返回带透明通道的位图。
    /// 图标内部的白色元素与边缘不相连，会被完整保留。
    /// </summary>
    public static Bitmap RemoveWhiteEdge(Bitmap src, int threshold)
    {
        var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.DrawImage(src, 0, 0, src.Width, src.Height);
        }

        int w = bmp.Width, h = bmp.Height;
        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        var bytes = new byte[stride * h];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

        var mark = new bool[w * h];
        var queue = new Queue<int>();

        // C# 5 兼容（PowerShell 5.1 Add-Type 编译器），用 lambda 代替本地函数
        Func<int, bool> IsWhite = (idx) =>
        {
            return bytes[idx] >= threshold
                && bytes[idx + 1] >= threshold
                && bytes[idx + 2] >= threshold;
        };

        Action<int, int> Push = (x, y) =>
        {
            if (x >= 0 && x < w && y >= 0 && y < h
                && !mark[y * w + x]
                && IsWhite(y * stride + x * 4))
            {
                mark[y * w + x] = true;
                queue.Enqueue(y * w + x);
            }
        };

        // 四边所有白色像素作为种子
        for (int x = 0; x < w; x++) { Push(x, 0); Push(x, h - 1); }
        for (int y = 0; y < h; y++) { Push(0, y); Push(w - 1, y); }

        // BFS 连通域
        while (queue.Count > 0)
        {
            int p = queue.Dequeue();
            int x = p % w, y = p / w;
            Push(x + 1, y); Push(x - 1, y); Push(x, y + 1); Push(x, y - 1);
        }

        // 连通白色区域 → 透明
        for (int m = 0; m < mark.Length; m++)
        {
            if (mark[m])
            {
                bytes[(m / w) * stride + (m % w) * 4 + 3] = 0;
            }
        }

        Marshal.Copy(bytes, 0, data.Scan0, bytes.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    /// <summary>
    /// 裁剪到内容包围盒（去掉去白后四周残留的透明空白），让图标主体尽量占满画布，
    /// 减小后续缩放时透明区与主体的混合面积。
    /// </summary>
    public static Bitmap CropToContent(Bitmap src)
    {
        int w = src.Width, h = src.Height;
        var rect = new Rectangle(0, 0, w, h);
        var data = src.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int stride = data.Stride;
        var bytes = new byte[stride * h];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (bytes[y * stride + x * 4 + 3] > 10)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }
        src.UnlockBits(data);

        if (maxX < minX) return src; // 全透明，原样返回

        // 保留主体周边约 8% 的透明边距：避免主体贴边（深色模式下贴边会显得有一圈边）
        int pad = Math.Max(4, (int)(Math.Min(w, h) * 0.08));
        minX = Math.Max(0, minX - pad);
        minY = Math.Max(0, minY - pad);
        maxX = Math.Min(w - 1, maxX + pad);
        maxY = Math.Min(h - 1, maxY + pad);
        return src.Clone(new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1), PixelFormat.Format32bppArgb);
    }
}
"@ -ReferencedAssemblies System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root $OutIco
if (-not (Test-Path $PngPath)) { throw "找不到图片: $PngPath" }

Write-Host "处理 $PngPath (阈值=$WhiteThreshold) ..."
$src = [System.Drawing.Image]::FromFile($PngPath)
$clean = [IconHelper]::RemoveWhiteEdge((New-Object System.Drawing.Bitmap($src)), $WhiteThreshold)
$src.Dispose()
# 裁剪内容包围盒：去掉四周空白，主体占满画布
$crop = [IconHelper]::CropToContent($clean)
$clean.Dispose()

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()   # 每个元素: @(尺寸, PNG字节)

try {
    foreach ($s in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($s, $s)
        try {
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.DrawImage($crop, 0, 0, $s, $s)
            $g.Dispose()

            # 关键：小尺寸缩放会与透明区混合产生「半透明白边光晕」，
            # 对每个尺寸再跑一次去白，把缩放产生的浅色边缘清掉。
            $final = [IconHelper]::RemoveWhiteEdge($bmp, 240)
            $bmp.Dispose()

            $ms = New-Object System.IO.MemoryStream
            $final.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $images += , @($s, $ms.ToArray())
            $ms.Dispose()
            $final.Dispose()
        } finally { if (-not $bmp.IsDisposed) { $bmp.Dispose() } }
    }
} finally { $crop.Dispose() }

# ---- 手工组装 ICO 文件（ICONDIR + ICONDIRENTRY[] + 图像数据）----
$count = $images.Count
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
try {
    # ICONDIR：reserved(0) / type(1=icon) / count
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$count)

    # ICONDIRENTRY
    $offset = 6 + 16 * $count
    foreach ($item in $images) {
        $s = $item[0]
        $data = $item[1]
        $dim = if ($s -ge 256) { 0 } else { $s }   # 256 用 0 表示
        $bw.Write([byte]$dim)
        $bw.Write([byte]$dim)
        $bw.Write([byte]0)                          # 调色板数
        $bw.Write([byte]0)                          # 保留
        $bw.Write([uint16]1)                        # 颜色平面
        $bw.Write([uint16]32)                       # 每像素位数
        $bw.Write([uint32]$data.Length)
        $bw.Write([uint32]$offset)
        $offset += $data.Length
    }

    # 图像数据
    foreach ($item in $images) { $bw.Write($item[1]) }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($out, $ms.ToArray())
    Write-Host "✅ 生成图标: $out ($((Get-Item $out).Length) bytes, $count 个尺寸, 白边已去除)"
} finally {
    $bw.Dispose()
    $ms.Dispose()
}
