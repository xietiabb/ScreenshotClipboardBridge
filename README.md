# 📸 Screenshot Clipboard Bridge

> **把「剪贴板里的截图」自动变成「本地图片文件路径」写回剪贴板** —— 专为 AI 客户端 + 视觉 MCP 场景设计。
>
> 截图 → 自动保存 PNG → 路径进剪贴板 → `Ctrl+V` 直接粘贴路径 → 视觉 MCP 按路径读图分析。

<p align="center">
  <img src="docs/screenshots/workflow.png" alt="一键工作流" width="90%">
</p>

[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-brightgreen)](https://www.microsoft.com/windows)
[![UI](https://img.shields.io/badge/UI-WinForms-8A2BE2)](https://learn.microsoft.com/dotnet/desktop/winforms/)
[![License](https://img.shields.io/badge/License-MIT-yellow)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen)](https://github.com/yourname/ScreenshotClipboardBridge/pulls)

---

## 为什么需要它

使用 AI 客户端（如 DPH）时，`Win + Shift + S` 截图后剪贴板里是**图片数据**，AI 客户端本身无法读取；
但你已经配置了视觉 MCP Server（GLM-4.6V-Flash），它**可以根据本地图片文件路径读取图片**。

这个工具就是中间的桥梁：**监听剪贴板 → 截图自动落盘 → 把完整路径写回剪贴板**。

```
1. 按 Win + Shift + S 框选截图
2. 本程序自动检测剪贴板中的图片
3. 自动保存为 %LOCALAPPDATA%\ScreenshotClipboardBridge\images\2026-08-15_12-45-33_a82f31.png
4. 自动把绝对路径以纯文本写回剪贴板
5. 直接进入 DPH 按 Ctrl + V
6. 输入框出现完整文件路径，视觉 MCP 读取并分析图片
```

## ✨ 功能特性

| 功能 | 说明 |
| --- | --- |
| 🖼️ 截图自动转换 | 检测到剪贴板图片 → 保存 PNG → 路径写回，**零操作** |
| 🛡️ 防死循环 | 程序自写路径会被识别跳过，绝不循环处理（详见 [架构文档](docs/ARCHITECTURE.md)） |
| ⏱️ 防抖去重 | 300ms 防抖，截图工具多次更新剪贴板也只保存一次 |
| 🚫 只处理图片 | 普通文本 / 代码 / 文件复制一律原样放行，绝不劫持 |
| 🧠 系统托盘 | 常驻后台，无主窗口；左键单击取最近路径、双击开设置 |
| ⚙️ 设置窗口 | 自动转换、开机启动、通知开关；截图目录、保存时间（1/3/7/30天/永久） |
| 🔔 系统通知 | 转换成功弹 Windows Toast（Win10/11），可关闭 |
| 🧹 缓存清理 | 只删本程序创建的截图（文件名白名单），**绝不碰用户其他文件** |
| 🔁 重启记忆 | 最近截图记录持久化，程序重启后仍可一键取回 |
| ⚡ 极轻量 | C# + .NET 8 + WinForms，无 Electron、无浏览器运行时，内存占用极低 |

## 🖥️ 界面一览

| 设置窗口 | 最近截图路径 |
| --- | --- |
| ![设置窗口](docs/screenshots/settings-window.png) | ![最近截图路径](docs/screenshots/path-dialog.png) |

> 托盘图标**左键单击**弹出「最近截图路径」对话框，路径已自动复制，直接 `Ctrl+V` 粘贴。

## 🚀 快速开始

### 方式一：直接运行（推荐）

1. 从 [Releases](../../releases) 下载 `ScreenshotClipboardBridge-win-x64.zip`（自包含版，无需安装 .NET）。
2. 解压，双击 `ScreenshotClipboardBridge.exe`。
3. 系统托盘出现图标即已常驻。
4. `Win + Shift + S` 截图 → 去 DPH `Ctrl + V` → 粘贴的就是图片绝对路径。

> 💡 想开机自启？托盘右键 →「开机自动启动」。

### 方式二：自行构建（需要 .NET 8 SDK）

```powershell
# 1. 还原 + 构建 + 测试
dotnet restore ScreenshotClipboardBridge.sln
dotnet build   ScreenshotClipboardBridge.sln -c Release

# 2. 发布 EXE
powershell -File scripts\build.ps1                # 框架依赖版（~25MB，需 .NET 8 Desktop Runtime）
powershell -File scripts\build.ps1 -SelfContained # 自包含版（~75MB，免装运行时）
```

> 📦 产物输出到 `dist\framework-dependent\` 与 `dist\self-contained\`。
> 首次发布会从 NuGet 还原依赖；离线受限网络可先运行 `node scripts\fetch-nuget.mjs` 下载本地包源（见 `nuget.config` 注释）。

## 🧭 使用说明

### 系统托盘（右键菜单）

- **📋 最近截图路径…** —— 打开路径对话框（路径已自动复制，可直接粘贴）
- **启用自动转换 / 暂停自动转换** —— 总开关（互斥显示）
- **打开截图保存目录** —— 资源管理器打开当前截图目录
- **设置** —— 打开设置窗口（托盘图标双击也可打开）
- **开机自动启动** —— 写注册表 `HKCU\...\Run`，无需管理员
- **清理缓存** —— 确认后删除本程序创建的全部截图
- **退出** —— 完全退出

### 设置窗口

| 分组 | 选项 |
| --- | --- |
| General | 自动转换截图 / 开机自动启动 / 转换成功通知 |
| Storage | 截图目录（路径 + 选择目录按钮，悬停可看完整路径）、保存时间（1/3/7/30 天、永久保存）、打开截图文件夹 |

## ⚙️ 配置文件

首次运行无需配置文件，默认值即满足要求。配置保存在：

```
%LOCALAPPDATA%\ScreenshotClipboardBridge\config.json
```

```json
{
  "enabled": true,            // 自动转换总开关
  "saveDirectory": "default", // "default"=默认目录，或自定义绝对路径
  "retentionDays": 7,         // 0=永久, 1, 3, 7, 30
  "notification": true,       // 转换成功通知
  "startup": false            // 开机自启（以注册表为准）
}
```

截图默认保存目录：`%LOCALAPPDATA%\ScreenshotClipboardBridge\images\`
最近截图记录：`%LOCALAPPDATA%\ScreenshotClipboardBridge\last-screenshot.json`

## 🗂️ 项目结构

```
Screenshot Clipboard Bridge/
├── src/ScreenshotClipboardBridge/     # 主程序（WinForms + .NET 8）
│   ├── App/                           # 应用级：路径常量、日志
│   ├── Clipboard/                     # 剪贴板监听、快照、图片处理管线
│   ├── Core/                          # 存储、文件名、防循环守卫
│   ├── Services/                      # 配置、开机自启、缓存清理、Toast 通知、最近记录
│   ├── Native/                        # P/Invoke 与 COM 互操作
│   ├── UI/                            # 托盘上下文、设置窗口、路径对话框、图标
│   └── assets/                        # 应用图标（scripts/make-icon.ps1 生成）
├── tests/ScreenshotClipboardBridge.Tests/  # xUnit 单元测试（36 个）
├── scripts/                           # build / smoke-test / make-icon / 离线引导脚本
├── docs/                              # 架构文档 + 界面截图
└── nuget.config                       # 双源配置（本地离线源 + nuget.org）
```

## ✅ 测试

### 单元测试（36 个）

覆盖：处理管线（只处理图片/防循环/连续截图）、文件名规则、存储清理安全、配置持久化、最近记录持久化、时间窗口守卫。

```powershell
dotnet build  ScreenshotClipboardBridge.sln
dotnet test   ScreenshotClipboardBridge.sln
```

### 端到端冒烟测试（11 项）

模拟验收标准的全部场景，自动启动程序并操作真实剪贴板：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-test.ps1 -ExePath .\dist\self-contained\ScreenshotClipboardBridge.exe
```

| 测试 | 场景 | 预期 |
| --- | --- | --- |
| Test 1 | 模拟截图（剪贴板放图片） | 自动保存 PNG，剪贴板变为文件路径 |
| Test 2 | 连续 10 次截图 | 10 个不重复 PNG，不丢失、不崩溃 |
| Test 3 | 复制普通文本 | 完全不处理，文本原样保留 |
| Test 4 | 复制代码 | 完全不处理，代码原样保留 |
| Test 5 | 复制文件（含图片格式+文件列表） | 完全不处理，文件列表原样保留 |
| Test 6 | 程序自写路径 | 无死循环，剪贴板稳定，进程存活 |

## ❓ FAQ

**Q：截图后去 DPH 粘贴，出来的不是路径？**
程序必须正在运行（托盘有图标）。左键单击托盘图标 → 打开「最近截图路径」对话框验证：显示路径=程序正常（问题在粘贴端）；显示"暂无记录"=截图时程序没运行。

**Q：托盘图标不见了？**
Win11 托盘图标可能收在 `^` 展开箭头里。也可在「设置 → 个性化 → 任务栏 → 系统托盘图标」中固定。

**Q：会误伤我复制的文本/文件吗？**
不会。处理管线只在「剪贴板含图片 且 不含文本 且 不含文件列表」时才执行，普通复制完全放行（冒烟测试 Test 3/4/5 验证）。

**Q：截图的保存时间怎么改？**
托盘右键 → 设置 → Storage → 保存时间（1/3/7/30 天 / 永久）。清理只删本程序创建的文件。

## 🔮 路线图

- [x] **v1.0** 截图 → 保存 → 路径写回剪贴板（当前版本）
- [ ] **v1.1** 截图历史浏览窗口、批量管理
- [ ] **v2.0** MCP 扩展：`get_latest_screenshot` / `read_latest_screenshot`，AI 免粘贴直接读图
- [ ] **v2.1** 跨设备同步、团队截图归档

## 📄 许可

[MIT](LICENSE)

---

**注意**：本工具只监听并转换剪贴板中的**图片**数据；普通文本、代码、文件复制均不受影响。
