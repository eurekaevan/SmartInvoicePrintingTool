using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Services.Implementations;
using Xunit;

namespace SmartInvoicePrintingTool.Tests;

public sealed class ProcessingOrchestratorTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"invoice-tool-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task ProcessAsync_UsesSelectedPrinter_ReportsBoundedProgress_AndAvoidsOverwrite()
    {
        var source = CreateDirectory("source");
        var output = CreateDirectory("output");
        var longPath = CreatePdf(source, "long.pdf");
        var shortPath = CreatePdf(source, "short.pdf");
        File.WriteAllText(Path.Combine(output, "long_short.pdf"), "existing");

        var printer = new FakePrintingService();
        var progressValues = new List<double>();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>
            {
                [longPath] = Metadata(longPath, 200, 600),
                [shortPath] = Metadata(shortPath, 400, 400)
            },
            printer,
            new SuccessfulMergingService());

        var result = await orchestrator.ProcessAsync(
            source,
            output,
            "Office Printer",
            new SynchronousProgress(progressValues.Add));

        Assert.Equal(1, result.MergeSucceeded);
        Assert.Equal(1, result.PrintSubmitted);
        Assert.Equal("Office Printer", printer.LastPrinterName);
        Assert.EndsWith("long_short_2.pdf", printer.LastPdfPath, StringComparison.Ordinal);
        Assert.All(progressValues, value => Assert.InRange(value, 0, 100));
        Assert.Equal(0, progressValues.First());
        Assert.Equal(100, progressValues.Last());
        Assert.Equal("existing", File.ReadAllText(Path.Combine(output, "long_short.pdf")));
    }

    [Fact]
    public async Task ProcessAsync_WhenMergeFails_ReturnsPartialFailureWithoutPrinting()
    {
        var source = CreateDirectory("source");
        var output = CreateDirectory("output");
        var longPath = CreatePdf(source, "long.pdf");
        var shortPath = CreatePdf(source, "short.pdf");
        var printer = new FakePrintingService();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>
            {
                [longPath] = Metadata(longPath, 200, 600),
                [shortPath] = Metadata(shortPath, 400, 400)
            },
            printer,
            new FailedMergingService());

        var result = await orchestrator.ProcessAsync(source, output, "Office Printer");

        Assert.True(result.HasFailures);
        Assert.Equal(1, result.MergeFailed);
        Assert.Equal(0, result.PrintSubmitted);
        Assert.Null(printer.LastPrinterName);
    }

    [Fact]
    public async Task ProcessAsync_WhenCancelled_PropagatesCancellation()
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
            orchestrator.ProcessAsync(source, output, "Office Printer", ct: cts.Token));
    }

    [Fact]
    public async Task ProcessAsync_RejectsSameSourceAndOutputDirectory()
    {
        var directory = CreateDirectory("same");
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>(),
            new FakePrintingService(),
            new SuccessfulMergingService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            orchestrator.ProcessAsync(directory, directory, "Office Printer"));
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
