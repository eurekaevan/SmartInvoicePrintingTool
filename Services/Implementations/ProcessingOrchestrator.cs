using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartInvoicePrintingTool.Services.Abstractions;

namespace SmartInvoicePrintingTool.Services.Implementations;

public class ProcessingOrchestrator : IProcessingOrchestrator
{
    private readonly IPdfMetadataService _metadataService;
    private readonly IPdfClassificationService _classificationService;
    private readonly IPdfPairMatchingService _pairMatchingService;
    private readonly IScaleCalculationService _scaleService;
    private readonly IPdfMergingService _mergingService;
    private readonly IPdfPrintingService _printingService;
    private readonly ILogSink _logSink;
    private readonly ILogger<ProcessingOrchestrator> _logger;

    public ProcessingOrchestrator(
        IPdfMetadataService metadataService,
        IPdfClassificationService classificationService,
        IPdfPairMatchingService pairMatchingService,
        IScaleCalculationService scaleService,
        IPdfMergingService mergingService,
        IPdfPrintingService printingService,
        ILogSink logSink,
        ILogger<ProcessingOrchestrator> logger)
    {
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        _classificationService = classificationService ?? throw new ArgumentNullException(nameof(classificationService));
        _pairMatchingService = pairMatchingService ?? throw new ArgumentNullException(nameof(pairMatchingService));
        _scaleService = scaleService ?? throw new ArgumentNullException(nameof(scaleService));
        _mergingService = mergingService ?? throw new ArgumentNullException(nameof(mergingService));
        _printingService = printingService ?? throw new ArgumentNullException(nameof(printingService));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessAsync(
        string sourceFolder, string outputFolder,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        _logSink.Log("开始处理...");

        // 1. 获取所有 PDF
        var pdfPaths = Directory.GetFiles(sourceFolder, "*.pdf");
        _logSink.Log($"找到 {pdfPaths.Length} 个 PDF 文件");
        if (pdfPaths.Length == 0)
        {
            _logSink.Log("未找到有效的 PDF 文件");
            progress?.Report(100);
            return;
        }

        // 2. 获取元数据
        var pdfs = new System.Collections.Generic.List<SmartInvoicePrintingTool.Models.PdfMetadata>();
        for (int i = 0; i < pdfPaths.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var meta = await _metadataService.GetMetadataAsync(pdfPaths[i], ct);
            if (meta != null) pdfs.Add(meta);
            progress?.Report((double)(i + 1) / pdfPaths.Length * 30);
        }

        // 3. 分类
        ct.ThrowIfCancellationRequested();
        var (longPdfs, shortPdfs) = _classificationService.ClassifyPdfs(pdfs);
        _logSink.Log($"分类结果: 长PDF={longPdfs.Count}, 短PDF={shortPdfs.Count}");

        // 4. 配对
        var pairs = _pairMatchingService.MatchPairs(longPdfs, shortPdfs);
        _logSink.Log($"匹配到 {pairs.Count} 对");
        if (pairs.Count == 0)
        {
            _logSink.Log("未配对到可合并的 PDF 文件组");
            progress?.Report(100);
            return;
        }

        // 5. 计算缩放并合并
        int processed = 0;
        foreach (var pair in pairs)
        {
            ct.ThrowIfCancellationRequested();
            var scales = _scaleService.CalculateScales(pair.LongPdf, pair.ShortPdf);
            if (scales == null)
            {
                _logSink.Log($"缩放失败: {pair.LongPdf.FileName} + {pair.ShortPdf.FileName}");
                continue;
            }

            pair.LongScale = scales.Value.LongScale;
            pair.ShortScale = scales.Value.ShortScale;

            var outputPath = System.IO.Path.Combine(outputFolder, pair.OutputFileName);
            var merged = await _mergingService.MergeAsync(
                pair.LongPdf.Path, pair.LongScale,
                pair.ShortPdf.Path, pair.ShortScale,
                outputPath, ct);

            if (merged)
                _logSink.Log($"合并成功: {pair.OutputFileName}");

            processed++;
            progress?.Report(30 + (double)processed / pairs.Count * 70);
        }

        _logSink.Log("处理完成！");
        progress?.Report(100);
    }
}