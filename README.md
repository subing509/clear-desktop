# 澄桌 Clear Desktop

一款面向 Windows 的轻量桌面 Dock 与应用启动台，以 macOS 的简洁视觉为参考，保留 Windows 的窗口操作习惯。

当前为实验性本地构建，尚非经过全面兼容性验证的稳定发行版。

## 给朋友：下载后安装

无需 Git、Python 或编译环境。适用于 64 位 Windows，当前主要在 Windows 11 验证。

1. [下载 Windows 安装包](https://github.com/subing509/clear-desktop/releases/download/v0.1.0-preview/ClearDesktop-Setup.exe)。
2. 双击 `ClearDesktop-Setup.exe`，点击“安装并启动”。
3. 安装完成后使用底部 Dock；左侧齿轮打开设置，可自行启用登录自启动。

安装到当前用户“文档”下的 `ClearDesktop` 文件夹，自动建立桌面与开始菜单快捷方式，不需要管理员权限。每位用户在自己的电脑上重新识别应用，不携带作者的分类、应用清单或路径。更新前先退出澄桌，再运行新安装包，保留 `data` 中的配置。

安装包尚未进行代码签名，Windows 可能提示“未知发布者”。请只从[本仓库 Release](https://github.com/subing509/clear-desktop/releases/tag/v0.1.0-preview) 下载，并按发布页的 `SHA256SUMS.txt` 核对文件；无法确认来源时不要运行。

也可下载同一发布页的 `ClearDesktop-Portable.zip`，解压到可写的固定目录后运行 `installed/ClearDesktop.exe`。如需移除：先在设置中取消登录自启动并退出，删除桌面、开始菜单快捷方式及安装目录；需要保留配置时先备份 `data`。

## 功能

- 半透明底部 Dock，原始应用图标、悬停放大与点击反馈。
- 应用启动台：名称搜索、分类筛选和分页。
- 设置页：批量添加 `.exe` / `.lnk`、调整应用分类、透明度与图标大小。
- 配置持久化，当前用户登录自启动入口。
- 支持部分真实窗口的最小化吸入和恢复过渡；无法读取画面的窗口回退为系统效果。
- 不替换 Explorer，不修改 Wallpaper 的壁纸与启动设置。

## 环境与构建

目前主要在 Windows 11、64 位环境验证。需要 Windows PowerShell 5.1、.NET Framework 4.x 和 WPF。Windows 10 与混合 DPI 多屏仍需更多测试。

在项目目录运行 Windows PowerShell：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

生成 `installed/ClearDesktop.exe`，双击即可运行。默认只显示 Dock，通过左侧设置图标打开设置。重复启动由互斥锁阻止。

构建不会自动启用开机启动；请在设置 → 通用中勾选“登录 Windows 后自动启动”。设置中的退出按钮关闭 Dock，自启动开关独立控制。

生成可分发的安装包与便携包：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\package.ps1
```

产物位于 `dist/0.1.0-preview/`。打包使用固定文件清单，并验证嵌入式程序文件，不包含本机配置、日志、截图或第三方图标。

## 配置与更新

配置保存于项目目录下的 `data/settings.json`，与 `installed` 同级。更改应用分类后点击“保存分类”；透明度和大小自动保存。首次运行使用默认配置，首次修改后创建配置文件。

请将项目放在可写的固定目录中。启用自启动后不要移动目录；移动后应重新设置自启动。更新前备份 `data`，不要覆盖个人配置。`.bak` 是上次保存的备份。

## 目录

- `native-preview/Start-ClearDesktop.ps1`：WPF 界面、应用发现与配置。
- `native-preview/InstalledHost.cs`：STA PowerShell 宿主与单实例控制。
- `native-preview/NativeIcons.cs`：原始应用图标读取。
- `native-preview/NativeMotion.cs`：Dock 与启动台动画。
- `native-preview/NativeGenie.cs`：窗口匹配、捕获和过渡。
- `native-preview/WindowRecoveryGuard.cs`：异常退出后的窗口状态恢复辅助程序。

## 已知限制与隐私

窗口过渡基于 PrintWindow 快照和真实窗口最小化/恢复，不是 macOS 原生窗口合成。受保护、提权、部分硬件加速窗口可能不支持；多窗口选择、系统任务栏弹出和多屏仍有兼容性限制。动画会按性能情况降低复杂度。

窗口画面仅在内存中短暂缓存，不保存截图、不上传。恢复辅助程序仅处理登记窗口的临时透明度与过渡状态。本项目没有遥测服务。应用配置含本机路径，请勿提交到公开仓库。

## 参与贡献

欢迎通过 Issues 提交复现步骤、Windows 版本、显示器缩放比例与目标应用名称。分享日志前请删除个人路径等信息。

## 许可证

代码使用 [MIT License](LICENSE)。应用名称、第三方图标及 Windows 组件的权利归各自所有者；仓库不分发第三方应用图标。
