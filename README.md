# 澄桌 Clear Desktop

一款面向 Windows 的轻量桌面 Dock 与应用启动台，以 macOS 的简洁视觉为参考，保留 Windows 的窗口操作习惯。

当前为实验性本地构建，尚非经过全面兼容性验证的稳定发行版。

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
