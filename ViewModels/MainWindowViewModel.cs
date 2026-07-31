using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartInvoicePrintingTool.Services.Abstractions;

namespace SmartInvoicePrintingTool.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    public Func<string, Task<string?>>? SelectFolder { get; set; }
    private readonly IProcessingOrchestrator _orchestrator;
    private readonly IPdfPrintingService _printingService;
    private readonly ILogSink _logSink;
    private CancellationTokenSource? _cts;
    private bool _isInitialized;
    private bool _isDisposed;

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
    public string LSystemReady => IsBusy
        ? (IsEnglish ? "PROCESSING" : "处理中")
        : (IsEnglish ? "SYSTEM READY" : "系统就绪");
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
        ILogSink logSink)
    {
        _orchestrator = orchestrator;
        _printingService = printingService;
        _logSink = logSink;

        _logSink.LogMessage += OnLogReceived;
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(LSystemReady));

    public async Task InitializeAsync()
    {
        if (_isInitialized || _isDisposed) return;
        _isInitialized = true;

        try
        {
            LogMessage("系统启动成功");
            var printers = await _printingService.GetAvailablePrintersAsync();
            Printers.Clear();
            foreach (var printer in printers)
            {
                Printers.Add(printer);
            }

            if (Printers.Count > 0)
                SelectedPrinter = Printers[0];
            else
                LogMessage("未检测到可用打印机");
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

        if (PathsEqual(SourcePath, OutputPath))
        {
            StatusMessage = IsEnglish ? "❌ Source and output directories cannot be the same!" : "❌ 源目录和输出目录不能相同！";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            StatusMessage = IsEnglish ? "❌ Please select an available printer first!" : "❌ 请先选择可用的打印机！";
            return;
        }

        var cts = new CancellationTokenSource();
        _cts = cts;
        IsBusy = true;
        IsCancellable = true;
        ProgressValue = 0;
        StatusMessage = IsEnglish ? "Processing... (Click stop to cancel)" : "正在处理中... (点击停止可取消)";

        try
        {
            var result = await _orchestrator.ProcessAsync(
                SourcePath,
                OutputPath,
                SelectedPrinter,
                new Progress<double>(p => ProgressValue = p),
                cts.Token);

            StatusMessage = FormatResult(result);
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
            if (ReferenceEquals(_cts, cts)) _cts = null;
            cts.Dispose();
        }
    }

    [RelayCommand]
    private void StopProcessing()
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

    private string FormatResult(Models.ProcessingResult result)
    {
        if (result.InputCount == 0)
            return IsEnglish ? "ℹ No PDF files found" : "ℹ 未找到 PDF 文件";
        if (result.PairCount == 0)
            return IsEnglish ? "ℹ No mergeable PDF pairs found" : "ℹ 未找到可合并的 PDF 配对";
        if (result.HasFailures)
        {
            return IsEnglish
                ? $"⚠ Completed with failures: merged {result.MergeSucceeded}, printed {result.PrintSubmitted}"
                : $"⚠ 处理完成但有失败：合并 {result.MergeSucceeded}，已提交打印 {result.PrintSubmitted}";
        }

        return IsEnglish
            ? $"✅ Completed: {result.MergeSucceeded} merged and submitted for printing"
            : $"✅ 处理完成：合并并提交打印 {result.MergeSucceeded} 份";
    }

    private static bool PathsEqual(string first, string second)
    {
        var firstPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first));
        var secondPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second));
        return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _logSink.LogMessage -= OnLogReceived;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        GC.SuppressFinalize(this);
    }
}
