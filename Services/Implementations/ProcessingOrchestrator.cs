using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;

namespace SmartInvoicePrintingTool.Services.Implementations;

public class ProcessingOrchestrator : IProcessingOrchestrator
{
    private readonly IPdfMetadataService _metadataService;
    private readonly IPdfPairMatchingService _pairMatchingService;
    private readonly IScaleCalculationService _scaleService;
    private readonly IPdfMergingService _mergingService;
    private readonly IPdfPrintingService _printingService;
    private readonly ILogSink _logSink;

    public ProcessingOrchestrator(
        IPdfMetadataService metadataService,
        IPdfPairMatchingService pairMatchingService,
        IScaleCalculationService scaleService,
        IPdfMergingService mergingService,
        IPdfPrintingService printingService,
        ILogSink logSink)
    {
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        _pairMatchingService = pairMatchingService ?? throw new ArgumentNullException(nameof(pairMatchingService));
        _scaleService = scaleService ?? throw new ArgumentNullException(nameof(scaleService));
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
            return new ProcessingResult(0, 0, 0, 0, 0, [], null);
        }

        // 2. 获取元数据
        var pdfs = new List<PdfMetadata>();
        for (int i = 0; i < pdfPaths.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var meta = await _metadataService.GetMetadataAsync(pdfPaths[i], ct);
            if (meta is { Width: > 0, Height: > 0 }) pdfs.Add(meta);
            progress?.Report((double)(i + 1) / pdfPaths.Length * 30);
        }

        // 3. 按高度降序后高低互补配对；奇数时最高 PDF 保留为单独打印项
        ct.ThrowIfCancellationRequested();
        var pairingResult = _pairMatchingService.MatchPairs(pdfs);
        var pairs = pairingResult.Pairs;
        var standalonePdf = pairingResult.StandalonePdf == null
            ? null
            : new StandalonePdfResult(
                Path.GetFileName(pairingResult.StandalonePdf.Path),
                pairingResult.StandalonePdf.Path);
        _logSink.Log($"按高度高低互补匹配到 {pairs.Count} 对");
        if (standalonePdf != null)
            _logSink.Log($"文件数为奇数，最高 PDF 将单独打印: {standalonePdf.FileName}");

        if (pairs.Count == 0 && standalonePdf == null)
        {
            _logSink.Log("没有可处理的 PDF 文件");
            progress?.Report(100);
            return new ProcessingResult(pdfPaths.Length, pdfs.Count, 0, 0, 0, [], null);
        }

        // 4. 计算缩放并合并
        int processed = 0;
        int mergeSucceeded = 0;
        int mergeFailed = 0;
        var pairResults = new List<MergeItemResult>(pairs.Count);

        foreach (var pair in pairs)
        {
            ct.ThrowIfCancellationRequested();
            var firstFileName = Path.GetFileName(pair.FirstPdf.Path);
            var secondFileName = Path.GetFileName(pair.SecondPdf.Path);
            var outputPath = Path.Combine(outputFolder, pair.OutputFileName);
            var scales = _scaleService.CalculateScales(pair.FirstPdf, pair.SecondPdf);
            if (scales == null)
            {
                _logSink.Log($"缩放失败: {pair.FirstPdf.FileName} + {pair.SecondPdf.FileName}");
                mergeFailed++;
                pairResults.Add(new MergeItemResult(
                    firstFileName,
                    secondFileName,
                    pair.OutputFileName,
                    outputPath,
                    false,
                    "无法计算合适的缩放比例"));
                processed++;
                ReportPairProgress(progress, processed, pairs.Count);
                continue;
            }

            pair.FirstScale = scales.Value.FirstScale;
            pair.SecondScale = scales.Value.SecondScale;

            var temporaryPath = Path.Combine(outputFolder, $".{Guid.NewGuid():N}.tmp.pdf");
            try
            {
                var merged = await _mergingService.MergeAsync(
                    pair.FirstPdf.Path, pair.FirstScale,
                    pair.SecondPdf.Path, pair.SecondScale,
                    temporaryPath, ct);

                if (!merged)
                {
                    mergeFailed++;
                    _logSink.Log($"合并失败: {pair.OutputFileName}");
                    pairResults.Add(new MergeItemResult(
                        firstFileName,
                        secondFileName,
                        pair.OutputFileName,
                        outputPath,
                        false,
                        "PDF 合并服务未能生成文件"));
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
                mergeFailed++;
                _logSink.Log($"处理失败: {pair.OutputFileName} ({ex.Message})");
                pairResults.Add(new MergeItemResult(
                    firstFileName,
                    secondFileName,
                    pair.OutputFileName,
                    outputPath,
                    false,
                    ex.Message));
            }
            finally
            {
                TryDelete(temporaryPath);
                processed++;
                ReportPairProgress(progress, processed, pairs.Count);
            }
        }

        _logSink.Log($"合并结束: 成功 {mergeSucceeded}，失败 {mergeFailed}");
        progress?.Report(100);
        return new ProcessingResult(
            pdfPaths.Length,
            pdfs.Count,
            pairs.Count,
            mergeSucceeded,
            mergeFailed,
            pairResults,
            standalonePdf);
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
                    _logSink.Log($"打印跳过，文件不存在: {Path.GetFileName(pdfPath)}");
                }
                else if (await _printingService.PrintAsync(pdfPath, printerName, ct))
                {
                    submitted++;
                    _logSink.Log($"已提交打印: {Path.GetFileName(pdfPath)} -> {printerName}");
                }
                else
                {
                    failed++;
                    _logSink.Log($"打印提交失败: {Path.GetFileName(pdfPath)}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logSink.Log($"打印提交失败: {Path.GetFileName(pdfPath)} ({ex.Message})");
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

    private static void ReportPairProgress(IProgress<double>? progress, int processed, int total) =>
        progress?.Report(30 + (double)processed / total * 70);

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
