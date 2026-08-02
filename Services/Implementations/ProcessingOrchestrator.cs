using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Utils;

namespace SmartInvoicePrintingTool.Services.Implementations;

public class ProcessingOrchestrator : IProcessingOrchestrator
{
    private readonly IPdfMetadataService _metadataService;
    private readonly IPdfPairMatchingService _pairMatchingService;
    private readonly IPdfMergingService _mergingService;
    private readonly IPdfPrintingService _printingService;
    private readonly ILogSink _logSink;

    public ProcessingOrchestrator(
        IPdfMetadataService metadataService,
        IPdfPairMatchingService pairMatchingService,
        IPdfMergingService mergingService,
        IPdfPrintingService printingService,
        ILogSink logSink)
    {
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        _pairMatchingService = pairMatchingService ?? throw new ArgumentNullException(nameof(pairMatchingService));
        _mergingService = mergingService ?? throw new ArgumentNullException(nameof(mergingService));
        _printingService = printingService ?? throw new ArgumentNullException(nameof(printingService));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
    }

    public async Task<ProcessingResult> MergeAsync(
        string sourceFolder, string outputFolder,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ValidateMergeArguments(sourceFolder, outputFolder);
        progress?.Report(0);
        _logSink.Log("开始扫描并合并 PDF...");

        // 1. 获取所有 PDF
        var pdfPaths = Directory.EnumerateFiles(sourceFolder)
            .Where(path => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        _logSink.Log($"找到 {pdfPaths.Length} 个 PDF 文件");
        if (pdfPaths.Length == 0)
        {
            _logSink.Log("未找到有效的 PDF 文件");
            progress?.Report(100);
            return new ProcessingResult(0, 0, 0, 0, 0, [], []);
        }

        // 2. 获取元数据
        var pdfs = new List<PdfMetadata>();
        for (int i = 0; i < pdfPaths.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var metadataResult = await _metadataService.GetMetadataAsync(pdfPaths[i], ct);
            if (metadataResult.Metadata != null)
            {
                pdfs.Add(metadataResult.Metadata);
            }
            else
            {
                _logSink.Log(
                    $"读取失败: {Path.GetFileName(pdfPaths[i])}；原因：{GetFailureReason(metadataResult.ErrorMessage)}");
            }
            progress?.Report((double)(i + 1) / pdfPaths.Length * 30);
        }

        // 3. 按高度高低互补动态配对；缩放失败时较长项单独成页，较短项回队重试
        ct.ThrowIfCancellationRequested();
        var pairingResult = _pairMatchingService.MatchPairs(pdfs);
        var pairs = pairingResult.Pairs;
        var standalonePlans = pairingResult.StandalonePdfs;
        _logSink.Log($"按高度高低互补匹配到 {pairs.Count} 对");
        foreach (var plan in standalonePlans)
        {
            _logSink.Log(
                $"安排单独成页: {Path.GetFileName(plan.Pdf.Path)}；原因：{plan.Reason}");
        }

        if (pairs.Count == 0 && standalonePlans.Count == 0)
        {
            _logSink.Log("没有可处理的 PDF 文件");
            progress?.Report(100);
            return new ProcessingResult(pdfPaths.Length, pdfs.Count, 0, 0, 0, [], []);
        }

        // 4. 生成合并页与单独 A4 页
        int processed = 0;
        int mergeSucceeded = 0;
        int mergeFailed = 0;
        var pairResults = new List<MergeItemResult>(pairs.Count);
        var standaloneResults = new List<StandalonePdfResult>(standalonePlans.Count);
        var totalOutputs = pairs.Count + standalonePlans.Count;

        foreach (var pair in pairs)
        {
            ct.ThrowIfCancellationRequested();
            var firstFileName = Path.GetFileName(pair.FirstPdf.Path);
            var secondFileName = Path.GetFileName(pair.SecondPdf.Path);
            var outputPath = Path.Combine(outputFolder, pair.OutputFileName);
            var temporaryPath = Path.Combine(outputFolder, $".{Guid.NewGuid():N}.tmp.pdf");
            try
            {
                var mergeResult = await _mergingService.MergeAsync(
                    pair.FirstPdf.Path, pair.FirstScale,
                    pair.SecondPdf.Path, pair.SecondScale,
                    temporaryPath, ct);

                if (!mergeResult.IsSuccess)
                {
                    var failureReason = GetFailureReason(mergeResult.ErrorMessage);
                    mergeFailed++;
                    _logSink.Log($"合并失败: {pair.OutputFileName}；原因：{failureReason}");
                    pairResults.Add(new MergeItemResult(
                        firstFileName,
                        secondFileName,
                        pair.OutputFileName,
                        outputPath,
                        false,
                        failureReason));
                    continue;
                }

                File.Move(temporaryPath, outputPath, overwrite: true);
                mergeSucceeded++;
                pairResults.Add(new MergeItemResult(
                    firstFileName,
                    secondFileName,
                    pair.OutputFileName,
                    outputPath,
                    true));
                _logSink.Log($"合并成功: {firstFileName} + {secondFileName} -> {pair.OutputFileName}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failureReason = GetFailureReason(ex.Message);
                mergeFailed++;
                _logSink.Log($"处理失败: {pair.OutputFileName}；原因：{failureReason}");
                pairResults.Add(new MergeItemResult(
                    firstFileName,
                    secondFileName,
                    pair.OutputFileName,
                    outputPath,
                    false,
                    failureReason));
            }
            finally
            {
                TryDelete(temporaryPath);
                processed++;
                ReportProcessingProgress(progress, processed, totalOutputs);
            }
        }

        foreach (var plan in standalonePlans)
        {
            ct.ThrowIfCancellationRequested();
            var sourceFileName = Path.GetFileName(plan.Pdf.Path);
            var outputFileName = $"single_{plan.Pdf.FileName}.pdf";
            var outputPath = Path.Combine(outputFolder, outputFileName);
            var temporaryPath = Path.Combine(outputFolder, $".{Guid.NewGuid():N}.tmp.pdf");

            try
            {
                var standaloneResult = await _mergingService.CreateStandaloneAsync(
                    plan.Pdf.Path,
                    PdfConstants.StandaloneScale,
                    temporaryPath,
                    ct);

                if (!standaloneResult.IsSuccess)
                {
                    var failureReason = GetFailureReason(standaloneResult.ErrorMessage);
                    standaloneResults.Add(new StandalonePdfResult(
                        sourceFileName,
                        outputFileName,
                        outputPath,
                        false,
                        plan.Reason,
                        failureReason));
                    _logSink.Log(
                        $"单独成页失败: {sourceFileName}；原因：{failureReason}；触发条件：{plan.Reason}");
                    continue;
                }

                File.Move(temporaryPath, outputPath, overwrite: true);
                standaloneResults.Add(new StandalonePdfResult(
                    sourceFileName,
                    outputFileName,
                    outputPath,
                    true,
                    plan.Reason));
                _logSink.Log(
                    $"单独成页成功: {sourceFileName} -> {outputFileName}；原因：{plan.Reason}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var failureReason = GetFailureReason(ex.Message);
                standaloneResults.Add(new StandalonePdfResult(
                    sourceFileName,
                    outputFileName,
                    outputPath,
                    false,
                    plan.Reason,
                    failureReason));
                _logSink.Log(
                    $"单独成页失败: {sourceFileName}；原因：{failureReason}；触发条件：{plan.Reason}");
            }
            finally
            {
                TryDelete(temporaryPath);
                processed++;
                ReportProcessingProgress(progress, processed, totalOutputs);
            }
        }

        var standaloneSucceeded = standaloneResults.Count(item => item.IsSuccess);
        var standaloneFailed = standaloneResults.Count - standaloneSucceeded;
        _logSink.Log(
            $"处理结束: 合并成功 {mergeSucceeded}，合并失败 {mergeFailed}，"
            + $"单独成页成功 {standaloneSucceeded}，单独成页失败 {standaloneFailed}");
        progress?.Report(100);
        return new ProcessingResult(
            pdfPaths.Length,
            pdfs.Count,
            pairs.Count,
            mergeSucceeded,
            mergeFailed,
            pairResults,
            standaloneResults);
    }

    public async Task<PrintResult> PrintAsync(
        IReadOnlyList<string> pdfPaths,
        string printerName,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(printerName);

        progress?.Report(0);
        if (pdfPaths.Count == 0)
        {
            _logSink.Log("没有可打印的 PDF 文件");
            progress?.Report(100);
            return new PrintResult(0, 0, 0);
        }

        _logSink.Log($"开始向 {printerName} 提交 {pdfPaths.Count} 个 PDF 文件...");
        var submitted = 0;
        var failed = 0;

        for (var index = 0; index < pdfPaths.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            var pdfPath = pdfPaths[index];
            try
            {
                if (!File.Exists(pdfPath))
                {
                    failed++;
                    _logSink.Log(
                        $"打印提交失败: {Path.GetFileName(pdfPath)}；原因：文件不存在或已被移动");
                }
                else
                {
                    var printResult = await _printingService.PrintAsync(pdfPath, printerName, ct);
                    if (printResult.IsSuccess)
                    {
                        submitted++;
                        _logSink.Log($"已提交打印: {Path.GetFileName(pdfPath)} -> {printerName}");
                    }
                    else
                    {
                        failed++;
                        _logSink.Log(
                            $"打印提交失败: {Path.GetFileName(pdfPath)}；原因：{GetFailureReason(printResult.ErrorMessage)}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logSink.Log(
                    $"打印提交失败: {Path.GetFileName(pdfPath)}；原因：{GetFailureReason(ex.Message)}");
            }
            finally
            {
                progress?.Report((double)(index + 1) / pdfPaths.Count * 100);
            }
        }

        _logSink.Log($"打印提交结束: 成功 {submitted}，失败 {failed}");
        progress?.Report(100);
        return new PrintResult(pdfPaths.Count, submitted, failed);
    }

    private static void ValidateMergeArguments(string sourceFolder, string outputFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);

        if (!Directory.Exists(sourceFolder))
            throw new DirectoryNotFoundException($"源目录不存在: {sourceFolder}");
        if (!Directory.Exists(outputFolder))
            throw new DirectoryNotFoundException($"输出目录不存在: {outputFolder}");

        var sourcePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceFolder));
        var outputPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputFolder));
        if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("源目录和输出目录不能相同。");
    }

    private static void ReportProcessingProgress(IProgress<double>? progress, int processed, int total) =>
        progress?.Report(30 + (double)processed / total * 70);

    private static string GetFailureReason(string? errorMessage) =>
        string.IsNullOrWhiteSpace(errorMessage) ? "服务未提供详细原因" : errorMessage;

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 临时文件清理失败不应覆盖原始处理结果。
        }
    }
}
