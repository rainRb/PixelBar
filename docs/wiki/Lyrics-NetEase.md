# 网易云音乐动态歌词

PixelBar 支持在像素屏上同步显示 **网易云音乐 PC 版** 歌词。在「动态歌词」页选择 **网易云音乐** 即可。

## 必需组件：InfLink-rs

官方网易云 PC 客户端**不提供可靠播放进度**，PixelBar **必须**配合 [apoint123/InfLink-rs](https://github.com/apoint123/inflink-rs) 使用（BetterNCM 插件）。

| 组件 | 说明 |
|------|------|
| BetterNCM | 网易云插件框架 |
| InfLink-rs | 向 Windows SMTC 上报进度、曲目与歌曲 ID |

### 一键安装（推荐）

在 PixelBar **动态歌词** 页选择「网易云音乐」，点击 **一键安装 InfLink-rs**：

1. 若未安装 BetterNCM → 自动下载/使用内置安装包并启动安装程序
2. 自动将 `InfLink-rs.plugin` 复制到 `C:\betterncm\plugins\`（或 `%LocalAppData%\BetterNCM\plugins`）
3. **完全退出并重启网易云音乐**
4. 诊断应显示：`InfLink-rs（ID …，进度 …）`

发布版已在 `Assets/NetEaseInfLink/` **内置** BetterNCM 安装程序与 `InfLink-rs.plugin`，一键安装默认离线可用。更新内置包：运行 `tools/fetch-netease-inflink-assets.ps1`。

### 手动安装

1. 安装 [BetterNCM Installer](https://github.com/std-microblock/BetterNCM-Installer/releases/latest)
2. 从 [InfLink-rs Releases](https://github.com/apoint123/inflink-rs/releases/latest) 下载 `InfLink-rs.plugin`
3. 复制到 `C:\betterncm\plugins\`
4. 重启网易云音乐

> 请使用 **apoint123/inflink-rs**，勿用旧版 InfinityLink。

## 功能概览

| 能力 | 说明 |
|------|------|
| 进度同步 | InfLink-rs → Windows SMTC（必需） |
| 歌曲 ID | SMTC `Genre` 字段 `NCM-{数字ID}` |
| 歌词来源 | 优先本地缓存 → 按 ID 请求 API → 无 ID 时才标题匹配 |
| 本地歌词缓存 | `%LocalAppData%\Netease\CloudMusic\Temp\{hash}` 等 |
| API 缓存 | `%LocalAppData%\PixelBar\NetEaseLyricCache\{id}.json` |
| YRC / LRC | 解析缓存并按进度取当前行 |
| 时间偏移 | 微调歌词早晚（毫秒） |

## 快速开始

1. Windows 10/11，USB 连接 PixelBar，关闭 TempoHub
2. 安装 **InfLink-rs**（见上）
3. 播放歌曲生成歌词缓存（完整播放一遍）
4. 动态歌词 → 网易云音乐 → 开启「启用歌词推送」

## 工作原理

```
网易云 + InfLink-rs
  → SMTC Genre: NCM-{歌曲ID} + 播放进度
  → 按 ID 查找本地 Temp / API 缓存
  → 本地无则请求 music.163.com/api/song/lyric
  → 解析 YRC / LRC
  → 推送到 PixelBar 像素屏
```

## 故障排除

| 现象 | 建议 |
|------|------|
| 诊断显示「缺少 InfLink-rs」 | 使用一键安装；重启网易云 |
| InfLink-rs 已装但无进度 | 确认 InfLink 设置中已启用 SMTC；重启客户端 |
| 歌词不对 / 错歌 | 确认诊断含 `ID xxx`；有 ID 时不再靠歌名猜缓存 |
| 缓存 0 首 | 完整播放一遍；或等待 API 拉取（需网络） |

## 相关

- [InfLink-rs](https://github.com/apoint123/inflink-rs)
- [BetterNCM](https://github.com/BetterNCM/BetterNCM)
- [QQ 音乐动态歌词](Lyrics-QQMusic)
