# 智能发票合并打印工具

基于 Avalonia UI 12 与 .NET 10 开发的桌面发票处理工具，用于批量识别、智能配对、合并排版和打印 PDF 发票。应用界面采用 Material Design 3 风格，仅提供中文界面。

## 核心功能

- Material Design 3 界面：使用清晰的层级、柔和的容器色、卡片和圆角操作按钮，突出主要操作流程。
- 双栏工作台：左侧配置目录并查看进度，右侧分别控制合并和打印操作。
- 智能发票配对与缩放：自动读取发票尺寸，匹配长短票并计算适合 A4 纸张的缩放比例。
- 独立合并与打印：先将配对发票居中合并到 A4 页面，确认结果后再单独提交打印。
- 可视化合并结果：以全宽卡片显示每组源 PDF 与输出文件，长文件名支持换行和悬浮查看。
- 安全替换输出：先写入临时文件，合并成功后替换输出目录中的同名文件。
- 运行日志：保留任务进度与异常信息，并支持一键清空。
- 全中文界面：界面文案、目录选择窗口和状态提示均使用中文，不提供语言切换功能。

## 技术栈

| 模块 | 技术 | 说明 |
| :--- | :--- | :--- |
| 应用框架 | Avalonia UI 12.0.4 | 桌面图形界面框架 |
| 运行时 | .NET 10 | 应用运行环境 |
| 架构模式 | MVVM、CommunityToolkit.Mvvm 8.4.2 | 属性通知与命令绑定 |
| PDF 处理 | PdfSharp 6.2.4 | PDF 元数据提取、缩放、绘制和合并 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | 服务注册与生命周期管理 |

## 项目结构

```text
SmartInvoicePrintingTool/
├── Assets/                        # 应用资源
├── Models/                        # 领域模型
├── Services/                      # PDF 处理与打印服务
│   ├── Abstractions/              # 服务接口
│   └── Implementations/           # 服务实现
├── ViewModels/                    # 界面状态与命令
├── Views/                         # Avalonia 界面
│   ├── Components/                # 顶栏、目录、状态、操作、日志和合并结果组件
│   └── MainWindow.axaml           # 主窗口布局
├── Utils/                         # 文件选择与常量工具
├── App.axaml                      # Material Design 主题资源与控件样式
└── Program.cs                     # 应用入口
```

## 环境要求

- .NET 10 SDK 或更高版本
- Windows 桌面环境
- 可用的 PDF 默认打开程序和打印设备

## 构建与运行

```bash
dotnet build
dotnet run
```

## 使用方法

1. 点击“选择目录”，指定包含发票 PDF 的源目录。
2. 指定合并后 PDF 的输出目录。源目录与输出目录不能相同。
3. 点击“开始合并”。应用会自动扫描、配对、缩放并生成合并 PDF；若输出目录已有同名文件，将在合并成功后替换它。
4. 在“合并结果”区域确认每组源 PDF 和输出文件。
5. 选择目标打印机，点击“打印合并结果”，仅提交本次成功合并的文件。
6. 可通过处理进度和运行日志查看任务状态；需要取消时点击“停止当前任务”。

“已提交打印”表示任务已经交给 Windows 关联的 PDF 程序，不代表物理打印已经完成。

## 自动化测试

```bash
dotnet test tests/SmartInvoicePrintingTool.Tests/SmartInvoicePrintingTool.Tests.csproj
```
