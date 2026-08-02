using System;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Utils;

namespace SmartInvoicePrintingTool.Services.Implementations;

public class ScaleCalculationService : IScaleCalculationService
{
    public ScaleCalculationResult CalculateScales(
        PdfMetadata firstPdf, PdfMetadata secondPdf)
    {
        // 与 smart_printer.py 一致：优先让较短发票保持更大的比例，
        // 再逐步缩小较长发票，只按两张发票的总高度判断能否放入 A4。
        for (var secondPercent = PdfConstants.ScaleMaxPercent;
             secondPercent >= PdfConstants.ScaleMinPercent;
             secondPercent--)
        {
            var secondScale = secondPercent / 100.0;
            for (var firstPercent = PdfConstants.ScaleMaxPercent;
                 firstPercent >= PdfConstants.ScaleMinPercent;
                 firstPercent--)
            {
                var firstScale = firstPercent / 100.0;
                var totalHeight = firstPdf.Height * firstScale
                    + secondPdf.Height * secondScale;

                if (totalHeight <= PdfConstants.A4Height)
                    return ScaleCalculationResult.Success(firstScale, secondScale);
            }
        }

        return ScaleCalculationResult.Failure(BuildFailureReason(firstPdf, secondPdf));
    }

    private static string BuildFailureReason(PdfMetadata firstPdf, PdfMetadata secondPdf)
    {
        var minimum = PdfConstants.StandaloneScale;
        var firstHeight = firstPdf.Height * minimum;
        var secondHeight = secondPdf.Height * minimum;
        var combinedHeight = firstHeight + secondHeight;

        return $"两张发票均缩小到 70% 后的总高度为 {combinedHeight:F1} 磅，"
            + $"仍超过 A4 高度 {PdfConstants.A4Height:F1} 磅";
    }
}
