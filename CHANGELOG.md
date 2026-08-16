# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 格式，
版本号遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [v1.0.0] - 2026-08-15

### 新增

- **核心功能**：监听剪贴板，检测到图片自动保存为 PNG 文件，并把绝对路径以纯文本写回剪贴板
  - 使用 Win32 `AddClipboardFormatListener` 原生事件监听（非轮询，零 CPU 空转）
  - 优先读取剪贴板原始 PNG 字节（零重编码），回退 DIB/Bitmap 重编码
  - 300ms 防抖，截图工具的多次剪贴板更新只保存一次
  - 文件名规则 `yyyy-MM-dd_HH-mm-ss_随机hex.png`，同秒不冲突
- **防死循环**：`LoopGuard` 时间窗口 + 内容比对，程序自写路径不会再次处理；
  窗口内若剪贴板变成新图片（快速连续截图）也绝不误伤
- **只处理图片**：图片格式 ∧ 无文件列表 ∧ 无文本 四条件判定；
  普通文本 / 代码 / 文件复制完全不受影响
- **系统托盘**：启用/暂停、打开目录、设置、开机自启、清理缓存、退出；
  左键单击弹「最近截图路径」对话框（自动复制路径）、双击开设置
- **设置窗口**：General（自动转换/开机启动/通知）+ Storage（目录/保存时间/打开文件夹）
- **通知**：Windows Toast（自动注册 AUMID）+ 托盘气泡回退
- **缓存管理**：按保留天数（1/3/7/30 天/永久）自动清理；清理仅删除本程序命名文件
- **持久化**：config.json 配置 + last-screenshot.json 最近截图记录（重启不丢）
- **自定义图标**：EXE/托盘/窗口标题栏统一使用 assets/app.ico（可脚本化生成）
- **调试模式**：`--layout-preview` 自动渲染窗口截图（开发用）

### 修复

- 高 DPI（125%/150%/175%）下复选框文字截断（.NET 8 CheckBox 默认 AutoSize=False）
- 设置窗口布局控件挤压/遮挡问题（改为 AutoSize 自适应布局）
- 底部按钮边框被容器裁剪（下边框消失）
- 图标缩放产生的半透明白边光晕
- 预览进程不退出导致 EXE 被锁、发布失败

### 测试

- 36 项 xUnit 单元测试全部通过
- 11 项端到端冒烟测试全部通过（截图→路径、防循环、文本/代码/文件放行）

## [Unreleased]

### 计划中

- 截图历史浏览窗口、批量管理
- MCP 扩展：`get_latest_screenshot` / `read_latest_screenshot`
- 跨设备同步、团队截图归档
