using System;
using System.Collections.Generic;
using System.Linq;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;

namespace SmartInvoicePrintingTool.Services.Implementations;

public class PdfPairMatchingService : IPdfPairMatchingService
{
    public PdfPairingResult MatchPairs(IReadOnlyList<PdfMetadata> pdfs)
    {
        ArgumentNullException.ThrowIfNull(pdfs);

        var sortedPdfs = pdfs
            .OrderByDescending(pdf => pdf.Height)
            .ThenBy(pdf => pdf.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var standalonePdf = sortedPdfs.Count % 2 == 1
            ? sortedPdfs[0]
            : null;
        var highestIndex = standalonePdf == null ? 0 : 1;
        var lowestIndex = sortedPdfs.Count - 1;
        var pairs = new List<PdfPair>();

        // 高低互补，尽量平衡每张 A4 上两张发票的总高度。
        while (highestIndex < lowestIndex)
        {
            pairs.Add(new PdfPair
            {
                FirstPdf = sortedPdfs[highestIndex],
                SecondPdf = sortedPdfs[lowestIndex]
            });

            highestIndex++;
            lowestIndex--;
        }

        return new PdfPairingResult(pairs, standalonePdf);
    }
}
