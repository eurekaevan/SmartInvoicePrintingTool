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
    public string SystemStatusText => IsBusy ? "正在处理" : "系统就绪";

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

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(SystemStatusText));

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
            var path = await SelectFolder("请选择源 PDF 文件夹");
            if (path != null) SourcePath = path;
        }
    }

    [RelayCommand]
    private async Task BrowseOutput()
    {
        if (SelectFolder != null)
        {
            var path = await SelectFolder("请选择输出 PDF 文件夹");
            if (path != null) OutputPath = path;
        }
    }

    [RelayCommand]
    private async Task StartProcessing()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(SourcePath) || !System.IO.Directory.Exists(SourcePath))
        {
            StatusMessage = "请先选择有效的源 PDF 目录。";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputPath) || !System.IO.Directory.Exists(OutputPath))
        {
            StatusMessage = "请先选择有效的输出 PDF 目录。";
            return;
        }

        if (PathsEqual(SourcePath, OutputPath))
        {
            StatusMessage = "源目录和输出目录不能相同。";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPrinter))
        {
            StatusMessage = "请先选择可用的打印机。";
            return;
        }

        var cts = new CancellationTokenSource();
        _cts = cts;
        IsBusy = true;
        IsCancellable = true;
        ProgressValue = 0;
        StatusMessage = "正在处理，可点击“停止当前任务”取消。";

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
            StatusMessage = "任务已停止。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"处理失败：{ex.Message}";
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
        StatusMessage = "正在停止任务…";
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
            return "未找到 PDF 文件。";
        if (result.PairCount == 0)
            return "未找到可合并的 PDF 配对。";
        if (result.HasFailures)
        {
            return $"处理完成但有部分失败：成功合并 {result.MergeSucceeded} 份，已提交打印 {result.PrintSubmitted} 份。";
        }

        return $"处理完成：已合并并提交打印 {result.MergeSucceeded} 份。";
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
