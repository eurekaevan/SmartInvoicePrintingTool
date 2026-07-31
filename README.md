# 🖨️ Smart Invoice Printing Tool (智能发票合并打印工具)

> **基于 Avalonia UI 12 与 .NET 10 打造的前卫桌面级智能发票识别合并、版面排版与批量打印控制终端。**

---

## ⚡ 核心亮点 (Key Features)

- 🎨 **前卫赛博霓虹视觉 (Futuristic Cyber Neon Aesthetic)**：
  - 采用渐变黑洞背景（`#06070C`）与三色极光光晕（`#EC4899` 桃红 / `#8B5CF6` 紫罗兰 / `#06B6D4` 荧光青），带来前卫、震撼且大气的桌面端 UI 体验。
- 📊 **双栏网格仪表盘布局 (58% : 42% Split-View Dashboard Grid)**：
  - 突破传统上下堆叠，采用双栏现代布局：左栏聚焦目录路由与进度，右栏集结设备选择、Hero 控制按键与控制台，秩序感强，舒展有张力。
- 🌐 **实时中英文双语切换 (i18n Localization)**：
  - 顶栏内置 `🌐 English / 🌐 中文` 一键无缝切换按键，全界面文本、占位符、弹窗及状态提示实时零延迟更新。
- 🔄 智能发票 A4 自动配对与缩放 (Smart Pair Matching & Scaling)**：
  - 自动读取与分类 PDF 发票尺寸，智能匹配长短票，计算 A4 纸最佳利用缩放比例（最大化打印清晰度），在一张 A4 纸上优雅并行合并排版两张发票，大幅节省纸张成本。
- 📜 **赛博 Terminal 监控终端 (Dev Terminal Console)**：
  - 嵌入式 macOS/Cyber 控制点终端，带状态颜色指示与快捷清空功能，实时捕获后台编排与处理日志。

---

## 🛠️ 技术栈 (Tech Stack)

| 领域 / 模块 | 技术选型 | 说明 |
| :--- | :--- | :--- |
| **应用框架** | [Avalonia UI 12.0.4](https://avaloniaui.net/) | 高性能跨平台 XAML 桌面 GUI 框架 |
| **运行时环境** | .NET 10.0 (C# 12) | 现代最新 .NET 运行时 |
| **架构模式** | MVVM (CommunityToolkit.Mvvm 8.4.2) | 属性自动生成、弱引用与 RelayCommand |
| **PDF 处理** | PdfSharp 6.2.4 | PDF 元数据提取、页面缩放绘制与合并导出 |
| **依赖注入** | Microsoft.Extensions.DependencyInjection | 松耦合 Service 托管架构 |

---

## 📂 项目结构 (Project Architecture)

```
SmartInvoicePrintingTool/
├── Assets/                        # 资源文件
├── Models/                        # 领域模型
│   ├── PdfMetadata.cs             # PDF 元数据定义 (尺寸、页数)
│   └── PdfPair.cs                 # 配对发票组数据模型
├── Services/                      # 核心业务服务层
│   ├── Abstractions/              # 服务接口定义 (ILogSink, IPdfMergingService...)
│   └── Implementations/           # 业务逻辑实现 (ProcessingOrchestrator, PdfPrintingService...)
├── ViewModels/                    # 视图模型
│   ├── ViewModelBase.cs           # ViewModel 抽象基类
│   └── MainWindowViewModel.cs     # 主界面 ViewModel (状态控制、i18n 字典、Command)
├── Views/                         # XAML 视图层
│   ├── Components/                # 顶栏、目录、状态、打印与日志独立面板
│   ├── MainWindow.axaml           # 背景与双栏仪表盘布局
│   └── MainWindow.axaml.cs        # 视图后台代码 (安全对话框绑定)
├── Utils/                         # 工具辅助类
│   ├── SafeFileDialogs.cs         # 现代 StorageProvider 文件夹选择器
│   └── PdfConstants.cs            # A4 尺寸常量定义
├── App.axaml                      # 全局主题样式、调色板与控件 Template
├── App.axaml.cs                   # 应用初始化与依赖注入容器注册
└── Program.cs                     # 应用入口点
```

---

## 🚀 快速开始 (Getting Started)

### 环境要求
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) 或更高版本
- Rider / Visual Studio 2022 / VS Code (需安装 C# & Avalonia 扩展)

### 构建与运行

1. **克隆项目到本地**：
   ```bash
   git clone <repository-url>
   cd SmartInvoicePrintingTool
   ```

2. **编译项目**：
   ```bash
   dotnet build
   ```

3. **运行应用**：
   ```bash
   dotnet run
   ```

---

## 🎯 基础使用流程

1. **配置源与输出目录**：
   - 点击 **“浏览源目录”** 选择存有发票 PDF 的文件夹。
   - 点击 **“浏览输出目录”** 选择合并后生成的 PDF 保存位置。
   - 源目录与输出目录必须不同；已有同名结果不会被覆盖，程序会自动追加序号。
2. **选择目标打印设备**：
   - 从 **“目标打印设备选择”** 下拉菜单中选择目标物理或虚拟打印机。
3. **语言切换（可选）**：
   - 点击顶栏右侧 **`🌐 English / 🌐 中文`** 按键，随心切换界面语言。
4. **开始合并与打印**：
   - 点击 **`▶ 开始合并并打印`** 主控制按钮，系统自动开始扫描、配对、计算缩放比、合并 PDF，并在 Terminal 控制台中实时输出日志。
   - “已提交打印”表示任务已交给 Windows 关联的 PDF 阅读器，不代表物理打印已经完成。

### 自动化验证

```bash
dotnet test tests/SmartInvoicePrintingTool.Tests/SmartInvoicePrintingTool.Tests.csproj
```

---

<p align="center">
  Crafted with ❤️ using Avalonia UI & .NET 10
</p>
