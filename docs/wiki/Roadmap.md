# 路线图

以下为计划中的功能；优先级与实现方式可能随逆向进展调整。

欢迎 [Issue / PR](https://github.com/traceless929/PixelBar/issues)；也鼓励开发者**直接基于 [PixelBar.Sdk](SDK-Development)** 先行实现原型，成熟后再合入主线。

## 计划功能

| 方向 | 目标 | 说明 |
|------|------|------|
| **RGB 神光同步** | 氛围灯与 PC 灯效生态联动 | 对接华硕 Aura Sync、微星 Mystic Light、技嘉 RGB Fusion 等 |
| **动态歌词 · 扩展** | 更多播放器 | **已支持 QQ 音乐、网易云**（见 [Lyrics-QQMusic](Lyrics-QQMusic)、[Lyrics-NetEase](Lyrics-NetEase)） |
| **协议补全** | 游戏/宠物/0x17 模板 | 依赖对 TempoHub `0x17` 帧上传流程的进一步逆向 |
| **体验优化** | 托盘、引导、歌词微调等 | 持续迭代 |

## 预期链路（草案）

### RGB 神光同步

```
系统 RGB 源 → 颜色与模式映射 → PixelBar RGB 灯光包（0x6B）
```

### 动态歌词

```
QQ：qrc 缓存 → 解密 → SMTC 进度 → 像素屏
网易云：InfLink-rs → NCM-ID → 缓存/API → SMTC 进度 → 像素屏
```

## 版本里程碑

### v0.0.4.1（当前）

| 功能 | CLI / SDK | 桌面客户端 |
|------|-----------|------------|
| 文字 + 布局 | ✅ | ✅ |
| RGB 灯光 | ✅ | ✅ |
| 时钟 / 频谱 / 屏色 | ✅ | ✅ |
| **QQ 音乐动态歌词** | — | ✅ |
| **网易云动态歌词** | — | ✅ |
| 切歌歌词识别修复 | — | ✅ |
| InfLink-rs 一键安装 | — | ✅ |
| 游戏/宠物/0x17 模板 | ❌ | ❌ |

### v0.0.4

网易云 + QQ 动态歌词、InfLink 一键安装、界面重构。

### v0.0.3 / v0.0.2

安装包、便携版、QQ 歌词首版。详见 [CHANGELOG](https://github.com/traceless929/PixelBar/blob/main/CHANGELOG.md)。

### v0.0.1

首版：文字、灯光、时钟、频谱、屏色、托盘与使用引导。

详见 [功能支持范围](Features)。
