# =====================================================================
# Screenshot Clipboard Bridge — 发布脚本
#
# 用法：
#   powershell -File scripts\build.ps1                 # 框架依赖单文件 EXE（需安装 .NET 8 Desktop Runtime）
#   powershell -File scripts\build.ps1 -SelfContained  # 自包含单文件 EXE（免装运行时，约 70-90MB）
#
# 输出：
#   dist\framework-dependent\ScreenshotClipboardBridge.exe
#   dist\self-contained\ScreenshotClipboardBridge.exe
#
# 说明：
#   - UseSharedCompilation=false：某些受限环境（沙箱/代理拦截）下 Roslyn 编译服务器
#     无法通过命名管道通信，禁用后改为进程内编译，兼容性最好。
# =====================================================================
param(
    [switch]$SelfContained,
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# ---- 定位 dotnet：优先用户级 .NET 8 引导安装（本机 SDK 8 所在），否则用 PATH ----
$dotnetPath = ''
$devDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet8\dotnet.exe'
if (Test-Path $devDotnet) { $dotnetPath = $devDotnet }
if (-not $dotnetPath) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $dotnetPath = $cmd.Source }
}
if (-not $dotnetPath) { throw '未找到 dotnet SDK 8，请先安装 .NET 8 SDK' }

# 用户级安装的 CLI 首次运行会写 %USERPROFILE%\.dotnet；重定向到临时目录避免权限问题。
if (-not $env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'dotnet-cli-home' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

$proj = Join-Path $root 'src\ScreenshotClipboardBridge\ScreenshotClipboardBridge.csproj'
$out = if ($SelfContained) { Join-Path $root 'dist\self-contained' } else { Join-Path $root 'dist\framework-dependent' }

Write-Host "== 发布 $([System.IO.Path]::GetFileName($proj)) -> $out (self-contained=$SelfContained) =="

$compressArg = if ($SelfContained) { '-p:EnableCompressionInSingleFile=true' } else { '' }

& $dotnetPath publish $proj -c Release -r $Runtime -o $out `
    --self-contained $(if ($SelfContained) { 'true' } else { 'false' }) `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    $compressArg `
    -p:DebugType=None -p:DebugSymbols=false `
    -p:UseSharedCompilation=false

if ($LASTEXITCODE -ne 0) { throw "publish 失败，exit=$LASTEXITCODE" }

$exe = Join-Path $out 'ScreenshotClipboardBridge.exe'
Write-Host "== 完成：$exe ($([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB) =="
