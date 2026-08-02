using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;

namespace SmartInvoicePrintingTool.Services.Implementations;

public class PdfPairMatchingService : IPdfPairMatchingService
{
    private readonly IScaleCalculationService _scaleService;

    public PdfPairMatchingService(IScaleCalculationService scaleService) =>
        _scaleService = scaleService ?? throw new ArgumentNullException(nameof(scaleService));

    public PdfPairingResult MatchPairs(IReadOnlyList<PdfMetadata> pdfs)
    {
        ArgumentNullException.ThrowIfNull(pdfs);

        var sortedPdfs = pdfs
            .OrderByDescending(pdf => pdf.Height)
            .ThenBy(pdf => pdf.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var pairs = new List<PdfPair>();
        var standalonePdfs = new List<StandalonePdfPlan>();

        if (sortedPdfs.Count % 2 == 1)
        {
            standalonePdfs.Add(new StandalonePdfPlan(
                sortedPdfs[0],
                "有效发票数量为奇数，最高发票不参与配对"));
            sortedPdfs.RemoveAt(0);
        }

        while (sortedPdfs.Count >= 2)
        {
            var longestPdf = sortedPdfs[0];
            var shortestPdf = sortedPdfs[^1];
            sortedPdfs.RemoveAt(sortedPdfs.Count - 1);
            sortedPdfs.RemoveAt(0);

            var scales = _scaleService.CalculateScales(longestPdf, shortestPdf);
            if (scales.IsSuccess)
            {
                pairs.Add(new PdfPair
                {
                    FirstPdf = longestPdf,
                    SecondPdf = shortestPdf,
                    FirstScale = scales.FirstScale,
                    SecondScale = scales.SecondScale
                });
                continue;
            }

            standalonePdfs.Add(new StandalonePdfPlan(
                longestPdf,
                $"与 {Path.GetFileName(shortestPdf.Path)} 缩放匹配失败：{scales.ErrorMessage}；"
                + $"较短发票 {Path.GetFileName(shortestPdf.Path)} 已放回队列继续匹配"));
            sortedPdfs.Add(shortestPdf);
            sortedPdfs = sortedPdfs
                .OrderByDescending(pdf => pdf.Height)
                .ThenBy(pdf => pdf.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (sortedPdfs.Count == 1)
        {
            standalonePdfs.Add(new StandalonePdfPlan(
                sortedPdfs[0],
                "配对回退后剩余一份发票"));
        }

        return new PdfPairingResult(pairs, standalonePdfs);
    }
}
