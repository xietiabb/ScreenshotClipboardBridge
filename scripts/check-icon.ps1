# 检查 ICO 各尺寸边缘透明度（开发调试用）
param([string]$IcoPath = "src\ScreenshotClipboardBridge\assets\app.ico")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$ico = Join-Path $root $IcoPath
$bytes = [System.IO.File]::ReadAllBytes($ico)
$count = [BitConverter]::ToUInt16($bytes, 4)

function Extract-Png([int]$index) {
    $entry = 6 + $index * 16
    $size = [BitConverter]::ToUInt32($bytes, $entry + 8)
    $offset = [BitConverter]::ToUInt32($bytes, $entry + 12)
    $png = New-Object byte[] $size
    [Array]::Copy($bytes, $offset, $png, 0, $size)
    return $png
}

function Check-Png($pngBytes, [string]$label) {
    $tmp = Join-Path $env:TEMP ('chk-' + [guid]::NewGuid().ToString('N') + '.png')
    [System.IO.File]::WriteAllBytes($tmp, $pngBytes)
    $bmp = New-Object System.Drawing.Bitmap($tmp)
    $w = $bmp.Width
    $h = $bmp.Height
    $cx = [int]($w / 2)
    $cy = [int]($h / 2)
    $wl = $w - 1
    $hl = $h - 1
    $pts = @(
        @(0, 0), @($cx, 1), @($cx, $cy), @(1, $cx),
        @($wl, 0), @(0, $hl), @($wl, $hl)
    )
    $parts = @()
    foreach ($pt in $pts) {
        $c = $bmp.GetPixel($pt[0], $pt[1])
        $parts += "($($pt[0]),$($pt[1]))A=$($c.A)"
    }
    Write-Host "$label ${w}x${h}: $($parts -join '  ')"
    $bmp.Dispose()
    Remove-Item $tmp -Force
}

Write-Host "ICO 尺寸数: $count"
for ($i = 0; $i -lt $count; $i++) {
    Check-Png (Extract-Png $i) "尺寸[$i]"
}
