using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartInvoicePrintingTool.ViewModels;
using SmartInvoicePrintingTool.Views;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Services.Implementations;

namespace SmartInvoicePrintingTool;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = ConfigureServices();
        Services = services;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = services.GetRequiredService<MainWindowViewModel>()
            };
            desktop.Exit += (_, _) => services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // 注册服务
        services.AddSingleton<IPdfMetadataService, PdfMetadataService>();
        services.AddSingleton<IPdfPairMatchingService, PdfPairMatchingService>();
        services.AddSingleton<IScaleCalculationService, ScaleCalculationService>();
        services.AddSingleton<IPdfMergingService, PdfMergingService>();
        services.AddSingleton<IPdfPrintingService, PdfPrintingService>();
        services.AddSingleton<ILogSink, ReactiveLogSink>();
        services.AddSingleton<IProcessingOrchestrator, ProcessingOrchestrator>();

        // 注册 ViewModel
        services.AddTransient<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
