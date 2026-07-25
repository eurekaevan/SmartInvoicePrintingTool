using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SmartInvoicePrintingTool.Services.Abstractions;

namespace SmartInvoicePrintingTool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public Func<string, Task<string?>>? SelectFolder { get; set; }
    private readonly IProcessingOrchestrator _orchestrator;
    private readonly IPdfPrintingService _printingService;
    private readonly ILogSink _logSink;
    private readonly ILogger<MainWindowViewModel> _logger;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private bool _isCancellable;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _statusMessage = "就绪 (等待操作...)";

    [ObservableProperty] private string _sourcePath = string.Empty;
    [ObservableProperty] private string _outputPath = string.Empty;

    [ObservableProperty] private string _logContent = string.Empty;
    [ObservableProperty] private bool _isEnglish;

    [RelayCommand]
    private void ToggleLanguage()
    {
        IsEnglish = !IsEnglish;
        OnPropertyChanged(string.Empty);
    }

    // 国际化 i18n 属性字典
    public string LSubtitle => IsEnglish ? "Batch Invoice Recognition & Smart Printing Control Center" : "智能发票批量识别合并 • 自动版面精印控制中枢";
    public string LSystemReady => IsEnglish ? "SYSTEM READY" : "系统就绪";
    public string LDirConfig => IsEnglish ? "Directory Setup" : "目录路由配置";
    public string LSourcePathLabel => IsEnglish ? "Source Invoice PDF Directory" : "源发票 PDF 包含目录";
    public string LSourcePathPlaceholder => IsEnglish ? "Click button on right to pick source folder..." : "点击右侧按钮选择发票源目录...";
    public string LBrowseSource => IsEnglish ? "Browse Source" : "浏览源目录";
    public string LOutputPathLabel => IsEnglish ? "Processed PDF Output Directory" : "处理完成 PDF 输出目录";
    public string LOutputPathPlaceholder => IsEnglish ? "Click button on right to pick output folder..." : "点击右侧按钮选择生成结果保存目录...";
    public string LBrowseOutput => IsEnglish ? "Browse Output" : "浏览输出目录";
    public string LPrinterSelect => IsEnglish ? "Target Printer Selection" : "目标打印设备选择";
    public string LPrinterPlaceholder => IsEnglish ? "Select printer device..." : "选择打印机设备...";
    public string LStartPrint => IsEnglish ? "Start Merge & Print" : "开始合并并打印";
    public string LForceStop => IsEnglish ? "Force Stop" : "强制终止";
    public string LStatusLabel => IsEnglish ? "Status:" : "执行状态:";
    public string LTerminalLog => IsEnglish ? "TERMINAL LOG MONITOR" : "实时控制台日志";
    public string LClearLog => IsEnglish ? "CLEAR" : "清空";
    public string LLangSwitchText => IsEnglish ? "🌐 中文" : "🌐 English";
    
    // 打印机相关
    public ObservableCollection<string> Printers { get; } = new();
    [ObservableProperty] private string? _selectedPrinter;

    public MainWindowViewModel(
        IProcessingOrchestrator orchestrator,
        IPdfPrintingService printingService,
        ILogSink logSink,
        ILogger<MainWindowViewModel> logger)
    {
        _orchestrator = orchestrator;
        _printingService = printingService;
        _logSink = logSink;
        _logger = logger;

        // 1. 订阅日志事件
        _logSink.LogMessage += OnLogReceived;
        LogMessage("系统启动成功");

        // 2. 初始化后台加载打印机列表
        Task.Run(LoadPrintersAsync);
    }

    private async Task LoadPrintersAsync()
    {
        try
        {
            var printers = await _printingService.GetAvailablePrintersAsync();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Printers.Clear();
                foreach (var p in printers)
                {
                    Printers.Add(p);
                }

                if (Printers.Count > 0)
                    SelectedPrinter = Printers[0]; // 默认选中第一个
            });
        }
        catch (Exception ex)
        {
            LogMessage($"加载打印机失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task BrowseSource()
    {
        if (SelectFolder != null)
        {
            var title = IsEnglish ? "Select Source PDF Folder" : "请选择源 PDF 文件夹";
            var path = await SelectFolder(title);
            if (path != null) SourcePath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseOutput()
    {
        if (SelectFolder != null)
        {
            var title = IsEnglish ? "Select Output PDF Folder" : "请选择输出 PDF 文件夹";
            var path = await SelectFolder(title);
            if (path != null) OutputPath = path;
        }
    }

    [RelayCommand]
    private async Task StartProcessing()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(SourcePath) || !System.IO.Directory.Exists(SourcePath))
        {
            StatusMessage = IsEnglish ? "❌ Please select a valid source PDF directory first!" : "❌ 请先选择有效的源 PDF 目录！";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputPath) || !System.IO.Directory.Exists(OutputPath))
        {
            StatusMessage = IsEnglish ? "❌ Please select a valid output PDF directory first!" : "❌ 请先选择有效的输出 PDF 目录！";
            return;
        }
        
        _cts = new CancellationTokenSource();
        IsBusy = true;
        IsCancellable = true;
        StatusMessage = IsEnglish ? "Processing... (Click stop to cancel)" : "正在处理中... (点击停止可取消)";

        try
        {
            await _orchestrator.ProcessAsync(SourcePath, OutputPath, new Progress<double>(p => ProgressValue = p * 100), _cts.Token);
            StatusMessage = IsEnglish ? "✅ Processing Completed!" : "✅ 处理完成！";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = IsEnglish ? "⏹ Task Stopped" : "⏹ 已停止任务";
        }
        catch (Exception ex)
        {
            StatusMessage = IsEnglish ? $"❌ Error: {ex.Message}" : $"❌ 错误: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsCancellable = false;
            _cts?.Dispose();
        }
    }

    [RelayCommand]
    private async Task StopProcessing()
    {
        _cts?.Cancel();
        StatusMessage = IsEnglish ? "Stopping task..." : "正在停止任务...";
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogContent = string.Empty;
    }
    private void OnLogReceived(object? sender, string e)
    {
        // UI 线程更新日志（Avalonia 控件通常要求在主线程操作）
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LogContent = e + Environment.NewLine + LogContent;
            // 保持日志量可控，只保留最近 5000 字符
            if (LogContent.Length > 5000)
                LogContent = LogContent.Substring(0, 5000);
        });
    }

    private void LogMessage(string msg)
    {
        _logSink.Log(msg);
    }
}