using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.ViewModels;
using Xunit;

namespace SmartInvoicePrintingTool.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        $"invoice-tool-viewmodel-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task PrintResults_IncludesSuccessfulMergesAndStandalonePdf()
    {
        var source = Path.Combine(_testRoot, "source");
        var output = Path.Combine(_testRoot, "output");
        Directory.CreateDirectory(source);
        Directory.CreateDirectory(output);
        var mergedPath = Path.Combine(output, "second_third.pdf");
        var standalonePath = Path.Combine(source, "tallest.pdf");
        var orchestrator = new CapturingOrchestrator(
            new ProcessingResult(
                3,
                3,
                1,
                1,
                0,
                [new MergeItemResult(
                    "second.pdf",
                    "third.pdf",
                    "second_third.pdf",
                    mergedPath,
                    true)],
                new StandalonePdfResult("tallest.pdf", standalonePath)));
        using var viewModel = new MainWindowViewModel(
            orchestrator,
            new FakePrintingService(),
            new FakeLogSink())
        {
            SourcePath = source,
            OutputPath = output,
            SelectedPrinter = "Office Printer"
        };

        await viewModel.StartMergingCommand.ExecuteAsync(null);
        await viewModel.PrintResultsCommand.ExecuteAsync(null);

        Assert.Equal([mergedPath, standalonePath], orchestrator.PrintedPaths);
        Assert.Equal(2, viewModel.PrintableFileCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, recursive: true);
    }

    private sealed class CapturingOrchestrator(ProcessingResult mergeResult) : IProcessingOrchestrator
    {
        public IReadOnlyList<string> PrintedPaths { get; private set; } = [];

        public Task<ProcessingResult> MergeAsync(
            string sourceFolder,
            string outputFolder,
            IProgress<double>? progress = null,
            CancellationToken ct = default) => Task.FromResult(mergeResult);

        public Task<PrintResult> PrintAsync(
            IReadOnlyList<string> pdfPaths,
            string printerName,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            PrintedPaths = pdfPaths.ToArray();
            return Task.FromResult(new PrintResult(pdfPaths.Count, pdfPaths.Count, 0));
        }
    }

    private sealed class FakePrintingService : IPdfPrintingService
    {
        public Task<IReadOnlyList<string>> GetAvailablePrintersAsync() =>
            Task.FromResult<IReadOnlyList<string>>(["Office Printer"]);

        public Task<PdfPrintSubmissionResult> PrintAsync(
            string pdfPath,
            string printerName,
            CancellationToken ct = default) =>
            Task.FromResult(PdfPrintSubmissionResult.Success());
    }

    private sealed class FakeLogSink : ILogSink
    {
        public event EventHandler<string>? LogMessage;

        public void Log(string message) => LogMessage?.Invoke(this, message);
    }
}
