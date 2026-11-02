# Clothesline (晾衣绳)

Clothesline 是一款轻量、优雅的跨平台桌面常驻暂存架与格式转换工作台。它将物理悬挂的拟物化晾衣绳、手绘藤编收纳篮与环形快速转换轮盘融为一体，为 Windows 与 macOS 用户提供随取随用、零干扰的文件中转流。

---

## 核心特性

- **物理悬挂体验**：支持重力下垂挠度计算（Sag Dynamics）与阻尼弹簧摆动（Spring Motion），文件挂载时呈现自然的力学回弹与晃动反馈。
- **手绘复古美学**：采用温暖的牛皮纸卡片、木质衣夹、手撕纸便签角标与藤编收纳篮，界面无遮挡时平滑收起至屏幕顶端。
- **智能边缘感应与避让**：
  - 鼠标碰触屏幕顶端平滑展开，移出自动收起。
  - 右上角原生窗口按键（最小化、最大化、关闭）深度避让，采用 Windows `WM_NCHITTEST` 原生透明穿透，绝不影响普通窗口操作。
- **随手收纳（洗衣篮）**：
  - 绳上卡片可随时拖入或点击解挂放入藤编洗衣篮，数字徽章实时同步。
  - 支持拖拽已挂卡片收纳、从外部直接拖入收纳，以及从收纳篮重新一键挂回晾衣绳。
- **无感安全存储**：
  - 用户拖入的本地文件绝不移动或复制，仅引用原生物理路径；
  - 移除卡片仅解除视觉挂载，绝不误删用户本地磁盘上的任何源文件。
- **格式快速转换轮盘**：
  - 鼠标拖拽并配合修饰键唤起环形转换菜单，涵盖 Office、图片、音视频、PDF 与数据格式转换。

---

## 解决方案结构

```text
Clothesline/
├── src/
│   ├── Clothesline.App/         # Avalonia UI 主程序、桌面视图与拟物手绘控件
│   ├── Clothesline.Core/        # 核心数据模型、存储持久化与物理弹簧模型
│   ├── Clothesline.Engines/     # 多格式转码引擎管道 (Image/Office/PDF/Media/Data)
│   ├── Clothesline.Platform/    # 跨平台适配层 (Win32 穿透避让/剪贴板/托盘/开机自启)
│   └── Clothesline.Tests/       # 自动化单元测试套件 (xUnit)
├── .gitignore                   # Git 忽略配置
├── Clothesline.sln              # 解决方案文件
└── Directory.Build.props        # 构建全局配置
```

---

## 构建与运行

### 前置要求
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本
- Windows 10/11 (x64/arm64) 或 macOS 12+

### 编译与运行
```bash
# 还原依赖并编译
dotnet build Clothesline.sln

# 运行自动化测试套件
dotnet test Clothesline.sln

# 运行桌面主程序
dotnet run --project src/Clothesline.App/Clothesline.App.csproj
```

### 独立发布 (Windows x64)
```bash
dotnet publish src/Clothesline.App/Clothesline.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o release/win-x64
```

### macOS 说明
- 晾衣绳挂在菜单栏下方；鼠标碰触屏幕顶端中段或拖拽文件靠近顶端即可唤出。
- 以菜单栏工具形式运行，不显示 Dock 图标，通过菜单栏托盘图标打开/退出。
- 窗口出现在所有桌面（Space）上；全屏应用所在 Space 中不会出现，天然避让全屏。
- 截屏到剪贴板（如 `Ctrl+Shift+Cmd+4`）会自动挂上晾衣绳；访达中复制文件不会被误判为截屏。
- 视频提取音频：安装 `ffmpeg`（`brew install ffmpeg`）后支持全部格式；未安装时仅支持 MP4/MOV/M4V 提取 WAV。
- Office 转 PDF 使用内置 OpenXML 引擎（Windows 上的 Office COM 无损导出不可用）。
- 点击卡片会激活 Clothesline 应用（macOS 下无法像 Windows 那样完全不抢焦点）。

---

## 交互指南

| 操作手势 | 行为说明 |
| :--- | :--- |
| **鼠标轻触屏幕顶端** | 平滑滑出晾衣绳；移开后自动收起至屏幕边缘外 |
| **文件拖入顶端** | 外部文件直接挂载上绳，带阻尼弹性晃动 |
| **单击卡片** | 原生复制文件路径到系统剪贴板 |
| **双击卡片** | 调用系统默认程序打开源文件 |
| **长按悬浮** | 自动呼出大尺寸多模态高清预览浮窗 |
| **点击卡片右上角 [×]** | 取下卡片放入洗衣篮（仅脱钩，不删本地原文件） |
| **卡片拖入右侧篮子** | 顺滑放入洗衣篮收纳，徽章计数自增 |
| **点击洗衣篮** | 打开历史收纳面板，支持重新挂回或清空 |

---

## 许可证

本项目基于 MIT 许可证开源。
# ClothesLine
