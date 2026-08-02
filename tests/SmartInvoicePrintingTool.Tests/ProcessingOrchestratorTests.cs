using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Services.Implementations;
using Xunit;

namespace SmartInvoicePrintingTool.Tests;

public sealed class ProcessingOrchestratorTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"invoice-tool-tests-{Guid.NewGuid():N}");

    [Fact]
    public void MatchPairs_WhenCountIsEven_PairsHighestWithLowest()
    {
        var service = new PdfPairMatchingService();
        var result = service.MatchPairs(
        [
            Metadata("100.pdf", 100, 100),
            Metadata("400.pdf", 100, 400),
            Metadata("200.pdf", 100, 200),
            Metadata("300.pdf", 100, 300)
        ]);

        Assert.Null(result.StandalonePdf);
        Assert.Collection(
            result.Pairs,
            pair =>
            {
                Assert.Equal("400", pair.FirstPdf.FileName);
                Assert.Equal("100", pair.SecondPdf.FileName);
            },
            pair =>
            {
                Assert.Equal("300", pair.FirstPdf.FileName);
                Assert.Equal("200", pair.SecondPdf.FileName);
            });
    }

    [Fact]
    public void MatchPairs_WhenCountIsOdd_LeavesTallestPdfForStandalonePrinting()
    {
        var service = new PdfPairMatchingService();
        var result = service.MatchPairs(
        [
            Metadata("second.pdf", 100, 400),
            Metadata("tallest.pdf", 100, 500),
            Metadata("middle.pdf", 100, 300),
            Metadata("second-shortest.pdf", 100, 200),
            Metadata("shortest.pdf", 100, 100)
        ]);

        Assert.Equal("tallest", result.StandalonePdf?.FileName);
        Assert.Collection(
            result.Pairs,
            pair =>
            {
                Assert.Equal("second", pair.FirstPdf.FileName);
                Assert.Equal("shortest", pair.SecondPdf.FileName);
            },
            pair =>
            {
                Assert.Equal("middle", pair.FirstPdf.FileName);
                Assert.Equal("second-shortest", pair.SecondPdf.FileName);
            });
    }

    [Fact]
    public async Task MergeAsync_WhenScalingFails_LogsConcreteConstraintReason()
    {
        var source = CreateDirectory("scale-failure-source");
        var output = CreateDirectory("scale-failure-output");
        var tooWidePath = CreatePdf(source, "too-wide.pdf");
        var normalPath = CreatePdf(source, "normal.pdf");
        var logSink = new FakeLogSink();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>
            {
                [tooWidePath] = Metadata(tooWidePath, 900, 500),
                [normalPath] = Metadata(normalPath, 200, 200)
            },
            new FakePrintingService(),
            new SuccessfulMergingService(),
            logSink);

        var result = await orchestrator.MergeAsync(source, output);

        Assert.Equal(1, result.MergeFailed);
        Assert.Contains(
            logSink.Messages,
            message => message.Contains("缩放失败", StringComparison.Ordinal)
                && message.Contains("原因", StringComparison.Ordinal)
                && message.Contains("70%", StringComparison.Ordinal)
                && message.Contains("A4", StringComparison.Ordinal));
    }

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
    public async Task MergeAsync_WhenCountIsOdd_ReturnsTallestPdfAsStandalonePrintItem()
    {
        var source = CreateDirectory("odd-source");
        var output = CreateDirectory("odd-output");
        var tallestPath = CreatePdf(source, "tallest.pdf");
        var middlePath = CreatePdf(source, "middle.pdf");
        var shortestPath = CreatePdf(source, "shortest.pdf");
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>
            {
                [tallestPath] = Metadata(tallestPath, 100, 700),
                [middlePath] = Metadata(middlePath, 100, 400),
                [shortestPath] = Metadata(shortestPath, 100, 200)
            },
            new FakePrintingService(),
            new SuccessfulMergingService());

        var result = await orchestrator.MergeAsync(source, output);

        Assert.Equal(tallestPath, result.StandalonePdf?.Path);
        Assert.Equal(1, result.PairCount);
        var pairResult = Assert.Single(result.PairResults);
        Assert.Equal("middle.pdf", pairResult.FirstFileName);
        Assert.Equal("shortest.pdf", pairResult.SecondFileName);
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
        var logSink = new FakeLogSink();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>
            {
                [longPath] = Metadata(longPath, 200, 600),
                [shortPath] = Metadata(shortPath, 400, 400)
            },
            printer,
            new FailedMergingService(),
            logSink);

        var result = await orchestrator.MergeAsync(source, output);

        Assert.True(result.HasFailures);
        Assert.Equal(1, result.MergeFailed);
        var pairResult = Assert.Single(result.PairResults);
        Assert.False(pairResult.IsSuccess);
        Assert.NotNull(pairResult.ErrorMessage);
        Assert.Equal("existing", File.ReadAllText(existingOutputPath));
        Assert.Null(printer.LastPrinterName);
        Assert.Contains(
            logSink.Messages,
            message => message.Contains("合并失败", StringComparison.Ordinal)
                && message.Contains("测试合并失败原因", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MergeAsync_WhenMetadataReadFails_LogsFileAndReason()
    {
        var source = CreateDirectory("metadata-failure-source");
        var output = CreateDirectory("metadata-failure-output");
        CreatePdf(source, "broken.pdf");
        var logSink = new FakeLogSink();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>(),
            new FakePrintingService(),
            new SuccessfulMergingService(),
            logSink);

        await orchestrator.MergeAsync(source, output);

        Assert.Contains(
            logSink.Messages,
            message => message.Contains("读取失败: broken.pdf", StringComparison.Ordinal)
                && message.Contains("测试元数据不存在", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrintAsync_WhenSubmissionFails_LogsServiceReason()
    {
        var output = CreateDirectory("print-failure-output");
        var pdfPath = CreatePdf(output, "merged.pdf");
        var logSink = new FakeLogSink();
        var orchestrator = CreateOrchestrator(
            new Dictionary<string, PdfMetadata>(),
            new FailedPrintingService(),
            new SuccessfulMergingService(),
            logSink);

        var result = await orchestrator.PrintAsync([pdfPath], "Office Printer");

        Assert.Equal(1, result.Failed);
        Assert.Contains(
            logSink.Messages,
            message => message.Contains("打印提交失败: merged.pdf", StringComparison.Ordinal)
                && message.Contains("测试打印失败原因", StringComparison.Ordinal));
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
        IPdfMergingService mergingService,
        ILogSink? logSink = null) =>
        new(
            new FakeMetadataService(metadata),
            new PdfPairMatchingService(),
            new ScaleCalculationService(),
            mergingService,
            printingService,
            logSink ?? new FakeLogSink());

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
        public Task<PdfMetadataReadResult> GetMetadataAsync(
            string pdfPath, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var value = metadata.GetValueOrDefault(pdfPath);
            return Task.FromResult(value == null
                ? PdfMetadataReadResult.Failure("测试元数据不存在")
                : PdfMetadataReadResult.Success(value));
        }
    }

    private sealed class SuccessfulMergingService : IPdfMergingService
    {
        public Task<PdfMergeResult> MergeAsync(
            string pdf1Path,
            double scale1,
            string pdf2Path,
            double scale2,
            string outputPath,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            File.WriteAllText(outputPath, "merged");
            return Task.FromResult(PdfMergeResult.Success());
        }
    }

    private sealed class FailedMergingService : IPdfMergingService
    {
        public Task<PdfMergeResult> MergeAsync(
            string pdf1Path,
            double scale1,
            string pdf2Path,
            double scale2,
            string outputPath,
            CancellationToken ct = default) =>
            Task.FromResult(PdfMergeResult.Failure("测试合并失败原因"));
    }

    private sealed class FakePrintingService : IPdfPrintingService
    {
        public string? LastPdfPath { get; private set; }
        public string? LastPrinterName { get; private set; }

        public Task<IReadOnlyList<string>> GetAvailablePrintersAsync() =>
            Task.FromResult<IReadOnlyList<string>>(["Office Printer"]);

        public Task<PdfPrintSubmissionResult> PrintAsync(
            string pdfPath, string printerName, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            LastPdfPath = pdfPath;
            LastPrinterName = printerName;
            return Task.FromResult(PdfPrintSubmissionResult.Success());
        }
    }

    private sealed class FailedPrintingService : IPdfPrintingService
    {
        public Task<IReadOnlyList<string>> GetAvailablePrintersAsync() =>
            Task.FromResult<IReadOnlyList<string>>(["Office Printer"]);

        public Task<PdfPrintSubmissionResult> PrintAsync(
            string pdfPath, string printerName, CancellationToken ct = default) =>
            Task.FromResult(PdfPrintSubmissionResult.Failure("测试打印失败原因"));
    }

    private sealed class FakeLogSink : ILogSink
    {
        public event EventHandler<string>? LogMessage;
        public List<string> Messages { get; } = [];

        public void Log(string message)
        {
            Messages.Add(message);
            LogMessage?.Invoke(this, message);
        }
    }

    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
