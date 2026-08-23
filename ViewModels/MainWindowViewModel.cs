using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InvoicePress.Models;
using InvoicePress.Services.Abstractions;

namespace InvoicePress.ViewModels;

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

    public string SystemStatusText => IsBusy ? "正在处理" : "系统就绪";
    public ObservableCollection<LogEntry> LogEntries { get; } = new();
    public bool HasLogEntries => LogEntries.Count > 0;
    public bool HasNoLogEntries => !HasLogEntries;
    public int LogEntryCount => LogEntries.Count;
    public ObservableCollection<MergeItemResult> MergeResults { get; } = new();
    public ObservableCollection<StandalonePdfResult> StandaloneResults { get; } = new();
    public bool HasStandaloneResults => StandaloneResults.Count > 0;
    public bool HasPairResults => MergeResults.Count > 0;
    public bool HasMergeResults => MergeResults.Count > 0 || HasStandaloneResults;
    public bool HasNoMergeResults => !HasMergeResults;
    public int SuccessfulMergeCount => MergeResults.Count(item => item.IsSuccess);
    public int SuccessfulStandaloneCount => StandaloneResults.Count(item => item.IsSuccess);
    public int FailedMergeCount => MergeResults.Count - SuccessfulMergeCount;
    public int FailedStandaloneCount => StandaloneResults.Count - SuccessfulStandaloneCount;
    public int PrintableFileCount => SuccessfulMergeCount + SuccessfulStandaloneCount;
    public string MergeSummaryText => HasMergeResults
        ? $"配对 {MergeResults.Count} 组（成功 {SuccessfulMergeCount}、失败 {FailedMergeCount}），"
            + $"单独成页 {StandaloneResults.Count} 份"
            + $"（成功 {SuccessfulStandaloneCount}、失败 {FailedStandaloneCount}）"
        : "合并完成后将在这里显示文件配对";
    public bool CanPrintResults =>
        !IsBusy && PrintableFileCount > 0 && !string.IsNullOrWhiteSpace(SelectedPrinter);

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

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(SystemStatusText));
        OnPropertyChanged(nameof(CanPrintResults));
    }

    partial void OnSelectedPrinterChanged(string? value) =>
        OnPropertyChanged(nameof(CanPrintResults));

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
    private async Task StartMerging()
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

        var cts = new CancellationTokenSource();
        _cts = cts;
        IsBusy = true;
        IsCancellable = true;
        ProgressValue = 0;
        StatusMessage = "正在扫描并合并 PDF，可点击“停止当前任务”取消。";
        MergeResults.Clear();
        StandaloneResults.Clear();
        NotifyMergeResultsChanged();

        try
        {
            var result = await _orchestrator.MergeAsync(
                SourcePath,
                OutputPath,
                new Progress<double>(p => ProgressValue = p),
                cts.Token);

            foreach (var item in result.PairResults)
                MergeResults.Add(item);

            foreach (var item in result.StandaloneResults)
                StandaloneResults.Add(item);

            NotifyMergeResultsChanged();
            StatusMessage = FormatMergeResult(result);
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
    private async Task PrintResults()
    {
        if (IsBusy) return;

        var pdfPaths = MergeResults
            .Where(item => item.IsSuccess)
            .Select(item => item.OutputPath)
            .ToList();
        pdfPaths.AddRange(StandaloneResults
            .Where(item => item.IsSuccess)
            .Select(item => item.OutputPath));

        if (pdfPaths.Count == 0)
        {
            StatusMessage = "请先完成 PDF 配对处理。";
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
        StatusMessage = $"正在提交 {pdfPaths.Count} 份 PDF 到打印机…";

        try
        {
            var result = await _orchestrator.PrintAsync(
                pdfPaths,
                SelectedPrinter,
                new Progress<double>(p => ProgressValue = p),
                cts.Token);

            StatusMessage = FormatPrintResult(result);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "打印提交已停止。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"打印失败：{ex.Message}";
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
        LogEntries.Clear();
        NotifyLogEntriesChanged();
    }

    private void OnLogReceived(object? sender, string e)
    {
        // UI 线程更新日志（Avalonia 控件通常要求在主线程操作）
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Insert(0, CreateLogEntry(e));
            while (LogEntries.Count > 200)
                LogEntries.RemoveAt(LogEntries.Count - 1);

            NotifyLogEntriesChanged();
        });
    }

    private static LogEntry CreateLogEntry(string text)
    {
        const int timestampLength = 8;
        if (text.Length > timestampLength && text[timestampLength] == ' ')
            return new LogEntry(text[..timestampLength], text[(timestampLength + 1)..]);

        return new LogEntry("--:--:--", text);
    }

    private void LogMessage(string msg)
    {
        _logSink.Log(msg);
    }

    private static string FormatMergeResult(ProcessingResult result)
    {
        if (result.InputCount == 0)
            return "未找到 PDF 文件。";
        if (result.PairCount == 0 && result.StandaloneResults.Count == 0)
            return "未找到可处理的有效 PDF。";
        if (result.HasFailures)
        {
            var standaloneFailed = result.StandaloneResults.Count(item => !item.IsSuccess);
            return $"处理完成但有部分失败：合并成功 {result.MergeSucceeded} 组，"
                + $"合并失败 {result.MergeFailed} 组，单独成页失败 {standaloneFailed} 份。";
        }

        return $"处理完成：生成 {result.MergeSucceeded} 份合并 PDF，"
            + $"{result.StandaloneResults.Count} 份单独 A4 PDF。";
    }

    private static string FormatPrintResult(PrintResult result) => result.HasFailures
        ? $"打印提交完成但有部分失败：已提交 {result.Submitted} 份，失败 {result.Failed} 份。"
        : $"已将 {result.Submitted} 份 PDF 提交到打印机。";

    private void NotifyMergeResultsChanged()
    {
        OnPropertyChanged(nameof(HasMergeResults));
        OnPropertyChanged(nameof(HasNoMergeResults));
        OnPropertyChanged(nameof(HasStandaloneResults));
        OnPropertyChanged(nameof(HasPairResults));
        OnPropertyChanged(nameof(SuccessfulMergeCount));
        OnPropertyChanged(nameof(SuccessfulStandaloneCount));
        OnPropertyChanged(nameof(FailedMergeCount));
        OnPropertyChanged(nameof(FailedStandaloneCount));
        OnPropertyChanged(nameof(PrintableFileCount));
        OnPropertyChanged(nameof(MergeSummaryText));
        OnPropertyChanged(nameof(CanPrintResults));
    }

    private void NotifyLogEntriesChanged()
    {
        OnPropertyChanged(nameof(HasLogEntries));
        OnPropertyChanged(nameof(HasNoLogEntries));
        OnPropertyChanged(nameof(LogEntryCount));
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
