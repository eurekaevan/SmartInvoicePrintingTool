using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Services.Implementations;
using Xunit;

namespace SmartInvoicePrintingTool.Tests;

public sealed class ProcessingOrchestratorTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"invoice-tool-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task MergeAsync_ReplacesExistingOutput_ReportsProgress_AndReturnsPairDetails()
    {
        var source = CreateDirectory("source");
        var output = CreateDirectory("output");
        var longPath = CreatePdf(source, "long.pdf");
        var shortPath = CreatePdf(source, "short.pdf");
        File.WriteAllText(Path.Combine(output, "long_short.pdf"), "existing");

        var progressValues = new List<double>();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>
            {
                [longPath] = Metadata(longPath, 200, 600),
                [shortPath] = Metadata(shortPath, 400, 400)
            },
            new FakePrintingService(),
            new SuccessfulMergingService());

        var result = await orchestrator.MergeAsync(
            source,
            output,
            new SynchronousProgress(progressValues.Add));

        Assert.Equal(1, result.MergeSucceeded);
        Assert.Equal("merged", File.ReadAllText(Path.Combine(output, "long_short.pdf")));
        Assert.False(File.Exists(Path.Combine(output, "long_short_2.pdf")));
        var pairResult = Assert.Single(result.PairResults);
        Assert.True(pairResult.IsSuccess);
        Assert.Equal("long.pdf", pairResult.FirstFileName);
        Assert.Equal("short.pdf", pairResult.SecondFileName);
        Assert.Equal("long_short.pdf", pairResult.OutputFileName);
        Assert.All(progressValues, value => Assert.InRange(value, 0, 100));
        Assert.Equal(0, progressValues.First());
        Assert.Equal(100, progressValues.Last());
    }

    [Fact]
    public async Task PrintAsync_UsesSelectedPrinter_AndReportsBoundedProgress()
    {
        var output = CreateDirectory("print-output");
        var mergedPath = CreatePdf(output, "merged.pdf");
        var printer = new FakePrintingService();
        var progressValues = new List<double>();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>(),
            printer,
            new SuccessfulMergingService());

        var result = await orchestrator.PrintAsync(
            [mergedPath],
            "Office Printer",
            new SynchronousProgress(progressValues.Add));

        Assert.Equal(1, result.Submitted);
        Assert.Equal(0, result.Failed);
        Assert.Equal("Office Printer", printer.LastPrinterName);
        Assert.Equal(mergedPath, printer.LastPdfPath);
        Assert.All(progressValues, value => Assert.InRange(value, 0, 100));
        Assert.Equal(0, progressValues.First());
        Assert.Equal(100, progressValues.Last());
    }

    [Fact]
    public async Task MergeAsync_WhenMergeFails_ReturnsPairFailureWithoutPrinting()
    {
        var source = CreateDirectory("source");
        var output = CreateDirectory("output");
        var longPath = CreatePdf(source, "long.pdf");
        var shortPath = CreatePdf(source, "short.pdf");
        var existingOutputPath = Path.Combine(output, "long_short.pdf");
        File.WriteAllText(existingOutputPath, "existing");
        var printer = new FakePrintingService();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>
            {
                [longPath] = Metadata(longPath, 200, 600),
                [shortPath] = Metadata(shortPath, 400, 400)
            },
            printer,
            new FailedMergingService());

        var result = await orchestrator.MergeAsync(source, output);

        Assert.True(result.HasFailures);
        Assert.Equal(1, result.MergeFailed);
        var pairResult = Assert.Single(result.PairResults);
        Assert.False(pairResult.IsSuccess);
        Assert.NotNull(pairResult.ErrorMessage);
        Assert.Equal("existing", File.ReadAllText(existingOutputPath));
        Assert.Null(printer.LastPrinterName);
    }

    [Fact]
    public async Task MergeAsync_WhenCancelled_PropagatesCancellation()
    {
        var source = CreateDirectory("source");
        var output = CreateDirectory("output");
        var pdfPath = CreatePdf(source, "invoice.pdf");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata> { [pdfPath] = Metadata(pdfPath, 200, 600) },
            new FakePrintingService(),
            new SuccessfulMergingService());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            orchestrator.MergeAsync(source, output, ct: cts.Token));
    }

    [Fact]
    public async Task MergeAsync_RejectsSameSourceAndOutputDirectory()
    {
        var directory = CreateDirectory("same");
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>(),
            new FakePrintingService(),
            new SuccessfulMergingService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            orchestrator.MergeAsync(directory, directory));
    }

    private ProcessingOrchestrator CreateOrchestrator(
        IReadOnlyDictionary<string, PdfMetadata> metadata,
        IPdfPrintingService printingService,
        IPdfMergingService mergingService) =>
        new(
            new FakeMetadataService(metadata),
            new PdfClassificationService(),
            new PdfPairMatchingService(),
            new ScaleCalculationService(),
            mergingService,
            printingService,
            new FakeLogSink());

    private string CreateDirectory(string name)
    {
        var path = Path.Combine(_testRoot, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreatePdf(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "fake pdf");
        return path;
    }

    private static PdfMetadata Metadata(string path, double width, double height) =>
        new() { Path = path, Width = width, Height = height };

    public void Dispose()
    {
        if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, recursive: true);
    }

    private sealed class FakeMetadataService(IReadOnlyDictionary<string, PdfMetadata> metadata) : IPdfMetadataService
    {
        public Task<PdfMetadata?> GetMetadataAsync(string pdfPath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(metadata.GetValueOrDefault(pdfPath));
        }
    }

    private sealed class SuccessfulMergingService : IPdfMergingService
    {
        public Task<bool> MergeAsync(
            string pdf1Path,
            double scale1,
            string pdf2Path,
            double scale2,
            string outputPath,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            File.WriteAllText(outputPath, "merged");
            return Task.FromResult(true);
        }
    }

    private sealed class FailedMergingService : IPdfMergingService
    {
        public Task<bool> MergeAsync(
            string pdf1Path,
            double scale1,
            string pdf2Path,
            double scale2,
            string outputPath,
            CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class FakePrintingService : IPdfPrintingService
    {
        public string? LastPdfPath { get; private set; }
        public string? LastPrinterName { get; private set; }

        public Task<IReadOnlyList<string>> GetAvailablePrintersAsync() =>
            Task.FromResult<IReadOnlyList<string>>(["Office Printer"]);

        public Task<bool> PrintAsync(string pdfPath, string printerName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            LastPdfPath = pdfPath;
            LastPrinterName = printerName;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeLogSink : ILogSink
    {
        public event EventHandler<string>? LogMessage;

        public void Log(string message) => LogMessage?.Invoke(this, message);
    }

    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
