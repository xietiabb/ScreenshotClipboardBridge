# =====================================================================
# PNG -> 多尺寸 ICO 转换脚本
# 生成 assets\app.ico（16/24/32/48/64/128/256 七档尺寸，内嵌 PNG 数据，
# Windows Vista+ 完全支持），用于：
#   - 程序 EXE 图标（csproj ApplicationIcon）
#   - 系统托盘图标（嵌入资源加载）
#
# 用法：powershell -File scripts\make-icon.ps1 -PngPath "C:\path\to\icon.png"
# =====================================================================
param(
    [Parameter(Mandatory = $true)][string]$PngPath,
    [string]$OutIco = "src\ScreenshotClipboardBridge\assets\app.ico"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root $OutIco
if (-not (Test-Path $PngPath)) { throw "找不到图片: $PngPath" }

$src = [System.Drawing.Image]::FromFile($PngPath)
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
            $g.DrawImage($src, 0, 0, $s, $s)
            $g.Dispose()

            $ms = New-Object System.IO.MemoryStream
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $images += , @($s, $ms.ToArray())
            $ms.Dispose()
        } finally { $bmp.Dispose() }
    }
} finally { $src.Dispose() }

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
    Write-Host "✅ 生成图标: $out ($((Get-Item $out).Length) bytes, $count 个尺寸)"
} finally {
    $bw.Dispose()
    $ms.Dispose()
}
