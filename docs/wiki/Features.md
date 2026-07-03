# 功能支持范围

## 已支持

| 功能 | CLI / SDK | 桌面客户端 |
|------|-----------|------------|
| 文字 + 布局（静态/滚动） | ✅ | ✅ |
| RGB 灯光（6 种模式） | ✅ | ✅ |
| 时钟样式 1–11 | ✅ | ✅ |
| 频谱样式 1–4 | ✅ | ✅ |
| 像素屏主题色 | ✅ | ✅ |
| **QQ 音乐动态歌词** | — | ✅（[说明](Lyrics-QQMusic)） |
| **网易云音乐动态歌词** | — | ✅（[说明](Lyrics-NetEase)，须 InfLink-rs） |
| 开机启动 / 托盘 | — | ✅ |
| 使用引导 | — | ✅ |

### 动态歌词（桌面客户端）

**QQ 音乐**

- 本地解密 `*_qm.qrc` 缓存（注册表 `CACHEPATH` 或自定义目录）
- SMTC 进度同步与外推、时间偏移微调
- 长句滚动；桌面歌词回退
- 解密缓存 `%LocalAppData%\PixelBar\LyricCache`

**网易云音乐**

- 须 BetterNCM + [InfLink-rs](https://github.com/apoint123/inflink-rs)（歌词页可一键安装）
- 按 SMTC 歌曲 ID 匹配本地缓存或 API 拉取 YRC/LRC
- 无 InfLink 时降级为歌名匹配（进度可能不准）

## 不支持

以下须使用官方 **EDIFIER TempoHub / Connect**：

- 游戏、办公、阅读、宠物、表情包、赛博等个性场景模板
- 自创空间图片/动画（`0x17` 帧上传）
- 热点新闻、天气预报
- TempoHub 新版 EF 时钟等需帧上传的样式

## 设备要求

- **设备**：漫步者花再 Halo PixelBar
- **VID/PID**：`0x2D99` / `0xA106`
- **系统**：Windows 10+ x64
- **连接**：USB

协议细节见仓库 [README · 协议格式](https://github.com/traceless929/PixelBar#协议格式)。
