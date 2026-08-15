# =====================================================================
# Screenshot Clipboard Bridge — 端到端冒烟测试
#
# 模拟验收标准中的核心场景（用程序设置剪贴板图片，等价于 Win+Shift+S）：
#   Test 1  截图 → 自动保存 PNG → 剪贴板变为文件路径
#   Test 2  连续 10 次截图 → 10 个不重复 PNG，不丢失、不崩溃
#   Test 3  复制普通文本 → 完全不处理
#   Test 4  复制代码 → 完全不处理
#   Test 5  复制文件（剪贴板同时含图片格式+文件列表）→ 完全不处理
#   Test 6  程序自写路径 → 无死循环（程序存活、剪贴板稳定）
#
# 用法（需在交互式用户会话中运行，剪贴板依赖会话）：
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-test.ps1 -ExePath <exe路径>
# 注意：请使用 Windows PowerShell 5.1（powershell.exe）运行，默认 STA 线程才能访问剪贴板。
# =====================================================================
param(
    [Parameter(Mandatory = $true)][string]$ExePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# 框架依赖运行：若本机通过 scripts\download-dotnet-sdk.mjs 装了用户级 .NET 8，
# 需要把 DOTNET_ROOT 指过去，apphost 才能找到 8.0 运行时（自包含 EXE 无需此项）。
if (-not $env:DOTNET_ROOT) {
    $devDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet8'
    if (Test-Path (Join-Path $devDotnet 'dotnet.exe')) { $env:DOTNET_ROOT = $devDotnet }
}

$passed = 0
$failed = 0

function Report([string]$name, [bool]$ok, [string]$detail = '') {
    if ($ok) { $script:passed++; Write-Host "[PASS] $name" }
    else { $script:failed++; Write-Host "[FAIL] $name $detail" }
}

function New-TestImage([int]$width, [int]$height, [string]$colorName) {
    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromName($colorName))
    $g.Dispose()
    return $bmp
}

Write-Host "== 启动程序: $ExePath =="
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 4
if ($proc.HasExited) {
    Write-Host "[FAIL] 程序启动即退出，ExitCode=$($proc.ExitCode)"
    exit 1
}
Write-Host "[INFO] 程序已启动 (PID $($proc.Id))"

# ---------------- Test 1：截图 → 保存 → 路径 ----------------
Write-Host "`n== Test 1：模拟 Win+Shift+S 截图 =="
$bmp = New-TestImage 320 200 'DodgerBlue'
[System.Windows.Forms.Clipboard]::SetImage($bmp)
$bmp.Dispose()
Start-Sleep -Seconds 3

$text = [System.Windows.Forms.Clipboard]::GetText()
Report "截图后剪贴板变成文件路径" ($text -match '\.png$') "剪贴板内容: [$text]"
Report "路径指向的文件真实存在" ($text -ne '' -and (Test-Path $text)) "路径: [$text]"
$firstPath = $text

# ---------------- Test 6：防死循环 ----------------
Start-Sleep -Seconds 2
$text2 = [System.Windows.Forms.Clipboard]::GetText()
Report "防死循环：剪贴板保持稳定（未被再次改写）" ($text2 -eq $firstPath) "当前: [$text2]"
Report "防死循环：程序进程存活" (-not $proc.HasExited)

# ---------------- Test 2：连续 10 次截图 ----------------
Write-Host "`n== Test 2：连续 10 次截图 =="
$imageDir = if ($firstPath) { Split-Path -Parent $firstPath } else { Join-Path $env:LOCALAPPDATA 'ScreenshotClipboardBridge\images' }
$before = @(Get-ChildItem -Path $imageDir -Filter '*.png' -ErrorAction SilentlyContinue).Count
$colors = @('Red','Green','Blue','Orange','Purple','Teal','Brown','Crimson','Gold','SlateGray')
for ($i = 0; $i -lt 10; $i++) {
    $b = New-TestImage (80 + $i * 10) (60 + $i * 10) $colors[$i]
    [System.Windows.Forms.Clipboard]::SetImage($b)
    $b.Dispose()
    Start-Sleep -Milliseconds 1200
}
$after = @(Get-ChildItem -Path $imageDir -Filter '*.png' -ErrorAction SilentlyContinue)
$delta = $after.Count - $before
Report "连续 10 次截图产生 10 个新 PNG（新增 $delta 个）" ($delta -eq 10)
Report "10 个文件名互不重复" (@($after | ForEach-Object Name | Sort-Object -Unique).Count -eq $after.Count)
$lastText = [System.Windows.Forms.Clipboard]::GetText()
Report "最后一次截图路径已写入剪贴板" ($lastText -match '\.png$')
Report "程序在连续截图后仍存活" (-not $proc.HasExited)

# ---------------- Test 3：普通文本 ----------------
Write-Host "`n== Test 3：复制普通文本 =="
[System.Windows.Forms.Clipboard]::SetText('hello world')
Start-Sleep -Seconds 2
$t3 = [System.Windows.Forms.Clipboard]::GetText()
Report "普通文本原样保留" ($t3 -eq 'hello world') "当前: [$t3]"

# ---------------- Test 4：代码文本 ----------------
Write-Host "`n== Test 4：复制代码 =="
[System.Windows.Forms.Clipboard]::SetText("print(`"hello`")`nconst x = 1;")
Start-Sleep -Seconds 2
$t4 = [System.Windows.Forms.Clipboard]::GetText()
Report "代码文本原样保留" ($t4 -eq "print(`"hello`")`nconst x = 1;") "当前: [$t4]"

# ---------------- Test 5：复制文件（图片格式+文件列表共存） ----------------
Write-Host "`n== Test 5：复制文件 =="
$tmpFile = Join-Path $env:TEMP ('scb-smoke-' + [guid]::NewGuid().ToString('N') + '.txt')
Set-Content -Path $tmpFile -Value 'dummy file'
$dataObject = New-Object System.Windows.Forms.DataObject
$dataObject.SetData([System.Windows.Forms.DataFormats]::FileDrop, [string[]]@($tmpFile))
$dataObject.SetImage((New-TestImage 50 50 'DarkSlateBlue'))
[System.Windows.Forms.Clipboard]::SetDataObject($dataObject, $true)
Start-Sleep -Seconds 2
$files5 = [System.Windows.Forms.Clipboard]::GetFileDropList()
$t5 = [System.Windows.Forms.Clipboard]::GetText()
Report "复制文件未被劫持（剪贴板仍是文件）" ($files5.Count -eq 1 -and $files5[0] -eq $tmpFile) "文本: [$t5] 文件数: $($files5.Count)"
Remove-Item -Path $tmpFile -Force -ErrorAction SilentlyContinue

# ---------------- 收尾 ----------------
Write-Host "`n== 收尾 =="
if (-not $proc.HasExited) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Write-Host "[INFO] 已关闭测试进程"
}
Write-Host "`n==============================================="
Write-Host "结果: 通过 $passed / $($passed + $failed)"
if ($failed -gt 0) { Write-Host "存在失败项！"; exit 1 }
Write-Host "全部通过 ✔"
exit 0
