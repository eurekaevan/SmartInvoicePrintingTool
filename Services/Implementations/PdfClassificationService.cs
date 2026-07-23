using System;
using System.Collections.Generic;
using System.Linq;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Utils;

namespace SmartInvoicePrintingTool.Services.Implementations;

public class PdfClassificationService : IPdfClassificationService
{
    public (List<PdfMetadata> LongPdfs, List<PdfMetadata> ShortPdfs) ClassifyPdfs(
        IReadOnlyList<PdfMetadata> pdfs)
    {
        var longPdfs = new List<PdfMetadata>();
        var shortPdfs = new List<PdfMetadata>();

        foreach (var pdf in pdfs)
        {
            if (pdf == null || pdf.Width <= 0 || pdf.Height <= 0) continue;

            // 宽长比小于阈值 = 长 PDF（如发票）
            var ratio = Math.Min(pdf.Width, pdf.Height) / Math.Max(pdf.Width, pdf.Height);
            if (ratio < PdfConstants.ClassificationThreshold)
                longPdfs.Add(pdf);
            else
                shortPdfs.Add(pdf);
        }

        return (longPdfs, shortPdfs);
    }
}