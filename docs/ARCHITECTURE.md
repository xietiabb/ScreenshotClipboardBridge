# Screenshot Clipboard Bridge — 架构文档

> 记录整体架构、模块职责、关键设计决策与扩展点。
> 读者：维护者 / 贡献者 / 想复用的开发者。

## 1. 总体架构

```
┌────────────────────────────────────────────────────────────────────┐
│                        Windows 用户会话                              │
│                                                                    │
│   Win+Shift+S                                                      │
│      │                                                             │
│      ▼                                                             │
│  ┌──────────┐    WM_CLIPBOARDUPDATE    ┌────────────────────────┐  │
│  │  系统剪贴板 │ ◄──────────────────────  │ ClipboardMonitor       │  │
│  │ (图片)    │                          │  (AddClipboardFormatListener)│
│  └──────────┘                          └───────────┬────────────┘  │
│        ▲                                            │ 事件(UI线程)    │
│        │ 写回文本路径                                ▼                │
│        │                                  ┌────────────────────────┐  │
│        └────────────────────────────────── │ TrayContext            │  │
│                                            │ · 防抖 300ms           │  │
│                                            │ · 调用处理管线          │  │
│                                            │ · 托盘菜单/设置窗口/通知 │  │
│                                            └───────────┬────────────┘  │
│                                                        ▼                │
│                                          ┌────────────────────────┐  │
│                                          │ ClipboardImageHandler  │  │
│                                          │ ① 防循环检查(LoopGuard) │  │
│                                          │ ② 只处理图片判定        │  │
│                                          │ ③ 取 PNG 字节           │  │
│                                          └───────┬────────┬───────┘  │
│                                                  ▼        ▼          │
│                                    ┌─────────────────┐  ┌──────────┐ │
│                                    │ ScreenshotStore │  │ 剪贴板写回 │ │
│                                    │ (存 PNG + 清理)  │  │ 文本路径  │ │
│                                    └─────────────────┘  └──────────┘ │
└────────────────────────────────────────────────────────────────────┘
```

## 2. 模块职责

| 模块 | 文件 | 职责 |
| --- | --- | --- |
| **入口** | `Program.cs` | STA 线程、单实例互斥、配置加载、管线组装、消息循环 |
| **App** | `AppPaths.cs` | 数据目录/配置路径/EXE 路径解析 |
| | `AppLog.cs` | 极简文件日志（启动状态与异常） |
| **Clipboard** | `ClipboardMonitor.cs` | `AddClipboardFormatListener` 原生监听（隐藏窗口收 `WM_CLIPBOARDUPDATE`） |
| | `WinClipboardSource.cs` | 剪贴板快照实现（格式探测 + PNG 字节提取，含重试与异常兜底） |
| | `IClipboardSource.cs` | 快照只读抽象（**可测试性的关键**） |
| | `ClipboardImageHandler.cs` | **核心管线**：防循环 → 只处理图片判定 → 保存 → 路径写回 |
| **Core** | `LoopGuard.cs` | 防死循环守卫（时间窗口 + 内容比对） |
| | `ScreenshotStore.cs` | 存储：保存/按保留天数清理/全清/打开目录（**文件名白名单安全**） |
| | `ScreenshotFileName.cs` | 文件名生成与「本程序文件」识别正则 |
| **Services** | `ConfigService.cs` | JSON 配置读写（camelCase、损坏容错、值规整） |
| | `StartupService.cs` | 开机自启（注册表 Run 键） |
| | `RetentionService.cs` | 定时清理（启动 20s 后 + 每小时） |
| | `ToastService.cs` | Windows Toast（AUMID 注册）+ 气泡回退 |
| **Native** | `NativeMethods.cs` | P/Invoke（剪贴板监听） |
| | `AppUserModelIdRegistrar.cs` | COM：开始菜单快捷方式写入 AUMID（Toast 前提） |
| **UI** | `TrayContext.cs` | ApplicationContext：托盘、菜单、防抖调度、设置窗口管理 |
| | `SettingsForm.cs` | 设置窗口（General / Storage） |
| | `TrayIcons.cs` | 运行时 GDI+ 绘制托盘图标 |

## 3. 关键设计决策

### 3.1 剪贴板监听：`AddClipboardFormatListener`（事件驱动，非轮询）

- 用一个隐藏 `NativeWindow` 调用 Win32 `AddClipboardFormatListener`，剪贴板一变即收到 `WM_CLIPBOARDUPDATE`。
- **零 CPU 空转**，符合「极轻量、低内存、长期后台」的定位。
- 兜底：若原生监听注册失败（极罕见），自动退化为 500ms 轻量轮询，功能不中断。

### 3.2 防死循环：`LoopGuard`（时间窗口 + 内容比对）

程序写回路径后，剪贴板变化事件会再次触发。判定逻辑：

```
是自写事件 ⇔ 距上次自写 < 3s
             ∧ 剪贴板「无图片」
             ∧ 有文本
             ∧ 文本 == 上次自写的路径
```

- **不用裸时间戳拦截**：用户在 3 秒内连续截两张图，剪贴板是图片 → 绝不误伤；
- 真正的循环风险（把图片写回剪贴板）在源头就不存在——程序只写文本。

### 3.3 只处理图片：四条件判定

处理管线在以下**全部**满足时才执行：

1. 不是程序自写事件（防循环）；
2. 剪贴板含图片格式（Bitmap / DIB / PNG / JFIF 任一）；
3. **不含文件列表**（`CF_HDROP`）——复制文件（哪怕图片文件）一律放行；
4. **不含文本**——普通文本/代码/富文本复制一律放行。

这样 `Ctrl+C` 普通文本、代码、文件的行为完全不受影响（冒烟测试 Test 3/4/5 验证）。

### 3.4 PNG 字节来源：优先原始格式，零重编码

1. 优先读剪贴板自带 `"PNG"` 格式的**原始字节**（截图工具原始编码，无二次压缩损失、最快）；
2. 回退 `Bitmap`/`DIB` → 内存中重编码为 PNG。

### 3.5 防抖（Debounce）

截图工具可能在极短时间内多次更新剪贴板（如依次写入 PNG/DIB 多种格式），
`WM_CLIPBOARDUPDATE` 会连续触发多次。统一折叠为 **300ms 窗口内一次处理**，
保证同一张截图只保存一次；处理中又来新事件则重排一次，保证不丢。

### 3.6 清理安全：文件名白名单

`ScreenshotFileName.IsOwnFile` 用严格正则匹配本程序命名（`yyyy-MM-dd_HH-mm-ss_6位hex.png`）。
`ScreenshotStore` 的 `Cleanup` / `ClearAll` **只删除匹配该模式的文件**，
即使用户把其他文件放进同一目录，也绝不误删。

### 3.7 通知：Windows Toast + 气泡回退

- Win10/11 的托盘气泡已被弱化，因此优先用 `Windows.UI.Notifications` Toast；
- Toast 的前提是「带 AUMID 的开始菜单快捷方式」，由 `AppUserModelIdRegistrar`
  在首次通知时自动创建（COM IShellLink + IPropertyStore）；
- 注册失败自动回退托盘气泡，通知是锦上添花，不影响核心功能。

### 3.8 线程模型

- 入口 `[STAThread]`：Windows 剪贴板与 WinForms 均要求 STA；
- 全部剪贴板操作跑在 UI 线程（WinForms Timer 回调），避免跨线程访问；
- `RetentionService` 的后台定时清理只做文件操作，天然线程安全。

### 3.9 可测试性

- 剪贴板通过 `IClipboardSource` 抽象注入，`ClipboardImageHandler` / `LoopGuard`
  可脱离真实剪贴板做单元测试（`tests/` 下用 `FakeClipboardSource`）；
- `ScreenshotStore` 目录由委托注入，测试用临时目录；
- `LoopGuard` 时钟可注入，时间窗口可精确断言。

## 4. MCP 扩展预留（Phase 2）

第一阶段刻意不做 MCP Server，保持「截图 → 路径」的单一职责。预留扩展点：

| 未来能力 | 现有基础 |
| --- | --- |
| `get_latest_screenshot()` | `ClipboardImageHandler.LastSavedPath / LastSavedAtUtc` 已在内存中维护 |
| `read_latest_screenshot` | `ScreenshotStore` 可扩展「按创建时间倒序查询」方法 |
| 历史查询 | `ScreenshotFileName` 的时间前缀天然支持按日期过滤 |
| MCP Server 宿主 | 可在 `Services/` 新增 `McpBridgeService`，复用 `ScreenshotStore`，不触碰剪贴板逻辑 |

建议的 Phase 2 形状（示意，不构成承诺）：

```
Services/
└── McpBridgeService.cs      # stdio MCP Server（tools: get_latest_screenshot）
    └── 依赖 ScreenshotStore（查询） + ClipboardImageHandler（最新状态）
```

## 5. 构建与测试

| 命令 | 说明 |
| --- | --- |
| `dotnet restore` | 还原（双源：本地离线源 + nuget.org） |
| `dotnet build -c Release` | 编译 |
| `dotnet test` | xUnit 单元测试（32 个） |
| `powershell -File scripts\build.ps1 [-SelfContained]` | 发布单文件 EXE 到 `dist/` |
| `scripts\smoke-test.ps1` | 端到端冒烟测试（操作真实剪贴板） |

> 特殊环境提示：本仓库支持「schannel 损坏 / 无法直连 nuget.org」的开发机
> （见 `scripts/fetch-nuget.mjs` 与 `scripts/download-dotnet-sdk.mjs` 的注释）。
