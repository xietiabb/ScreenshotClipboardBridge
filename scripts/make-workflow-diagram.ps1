# =====================================================================
# 生成 README 用的「工作流程示意图」（docs/screenshots/workflow.png）
# 用 System.Drawing 绘制，无需任何外部依赖。
# =====================================================================
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root 'docs\screenshots'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'workflow.png'

$width = 920
$height = 240
$bmp = New-Object System.Drawing.Bitmap($width, $height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::FromArgb(250, 251, 253))

$fontTitle = New-Object System.Drawing.Font('Microsoft YaHei UI', 12, [System.Drawing.FontStyle]::Bold)
$fontStep  = New-Object System.Drawing.Font('Microsoft YaHei UI', 9.5, [System.Drawing.FontStyle]::Bold)
$fontSub   = New-Object System.Drawing.Font('Microsoft YaHei UI', 8)
$blue      = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 120, 215))
$green     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(46, 160, 67))
$dark      = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(51, 51, 51))
$white     = [System.Drawing.Brushes]::White
$arrowPen  = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 120, 120), 2)
$arrowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$arrowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round

# 标题
$g.DrawString('Screenshot Clipboard Bridge — 一键工作流', $fontTitle, $dark, 20, 16)

# 步骤定义: (标题, 副标题, x)
$steps = @(
    @('① 按 Win+Shift+S', '框选截图', 30),
    @('② 剪贴板出现图片', '程序自动监听', 210),
    @('③ 自动保存 PNG', 'images\时间戳.png', 390),
    @('④ 路径写回剪贴板', '纯文本绝对路径', 570),
    @('⑤ DPH 里 Ctrl+V', '粘贴出完整路径', 750)
)
$boxW = 140
$boxH = 72
$boxY = 96

foreach ($s in $steps) {
    $x = $s[2]
    $rect = New-Object System.Drawing.Rectangle($x, $boxY, $boxW, $boxH)
    $isLast = ($s[0] -like '⑤*')
    $fill = if ($isLast) { $green } else { $blue }
    $g.FillRectangle($fill, $rect)
    $g.DrawString($s[0], $fontStep, $white, ($x + 10), ($boxY + 10))
    $g.DrawString($s[1], $fontSub, $white, ($x + 10), ($boxY + 40))
}

# 箭头
foreach ($s in $steps) {
    $x = $s[2]
    if ($x -gt 30) {
        $y1 = $boxY + $boxH / 2
        $x1 = $x - 10
        $x2 = $x - 40
        $g.DrawLine($arrowPen, $x1, $y1, $x2, $y1)
    }
}

$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "✅ 流程图已生成: $out ($((Get-Item $out).Length) bytes)"
