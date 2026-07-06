花再 **Halo PixelBar** 第三方控制工具。适用于 Windows 10/11 x64，USB 连接设备（VID/PID `0x2D99` / `0xA106`）。

## 我该下载哪个？

| 文件 | 适合谁 | 说明 |
|------|--------|------|
| **PixelBar.App-v{VERSION}-setup.exe** | 普通用户（推荐） | 标准安装包：含系统环境检测与安装引导，写入开始菜单，支持卸载。 |
| **PixelBar.App-v{VERSION}-win-x64-portable.exe** | 免安装 / 高级用户 | 单文件便携版，下载后双击运行；若被 SmartScreen 拦截，请右键「属性 → 解除锁定」。 |
| **pixelbar-v{VERSION}-win-x64.exe** | 命令行 / 脚本用户 | CLI 工具，例如 `pixelbar text "你好"`、`pixelbar screen-color "#0077EE"`。 |
| **PixelBar.Sdk.{VERSION}.nupkg** | 开发者 | .NET SDK NuGet 包。可在 [GitHub Packages](https://github.com/traceless929/PixelBar/packages) 安装，或从此 Release 直接下载。 |
| **Source code (zip / tar.gz)** | 开发者 / 研究者 | GitHub 自动附带的对应标签源码归档，用于自行编译或阅读实现。 |

## v{VERSION} 亮点

- **修复切歌后歌词错歌**（网易云 + QQ 音乐）：InfLink 歌曲 ID 优先、SMTC 变化检测、QQ 桌面歌词反查
- 含 v0.0.4 全部能力：网易云/QQ 动态歌词、InfLink 一键安装、深色 UI
- 含标准安装包、便携版、CLI 与 SDK

详细说明：
- [Wiki · QQ 音乐动态歌词](https://github.com/traceless929/PixelBar/wiki/Lyrics-QQMusic)
- [Wiki · 网易云音乐动态歌词](https://github.com/traceless929/PixelBar/wiki/Lyrics-NetEase)

## 使用提示

- 使用前请用 USB 连接 PixelBar，并在应用 **设置** 中选择设备。
- 动态歌词请先**关闭 EDIFIER TempoHub**。
- 网易云须安装 InfLink-rs 并重启客户端；QQ 音乐需本地 qrc 缓存。
- 完整功能与固件升级仍请优先使用官方 TempoHub / Connect。

## 变更记录

https://github.com/traceless929/PixelBar/blob/main/CHANGELOG.md#0041---2026-07-06

完整提交：https://github.com/traceless929/PixelBar/commits/v{VERSION}
