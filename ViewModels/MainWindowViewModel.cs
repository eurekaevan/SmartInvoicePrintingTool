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
            StatusMessage = "❌ 请先选择有效的源 PDF 目录！";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputPath) || !System.IO.Directory.Exists(OutputPath))
        {
            StatusMessage = "❌ 请先选择有效的输出 PDF 目录！";
            return;
        }
        
        _cts = new CancellationTokenSource();
        IsBusy = true;
        IsCancellable = true;
        StatusMessage = "正在处理中... (点击停止可取消)";

        try
        {
            await _orchestrator.ProcessAsync(SourcePath, OutputPath, new Progress<double>(p => ProgressValue = p * 100), _cts.Token);
            StatusMessage = "✅ 处理完成！";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "⏹ 已停止任务";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 错误: {ex.Message}";
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
        StatusMessage = "正在停止任务...";
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