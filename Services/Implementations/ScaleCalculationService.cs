using System;
using System.Collections.Generic;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Utils;

namespace SmartInvoicePrintingTool.Services.Implementations;

public class ScaleCalculationService : IScaleCalculationService
{
    public ScaleCalculationResult CalculateScales(
        PdfMetadata firstPdf, PdfMetadata secondPdf)
    {
        for (double firstScale = PdfConstants.ScaleMax;
             firstScale >= PdfConstants.ScaleMin;
             firstScale -= PdfConstants.ScaleStep)
        {
            var firstWidth = firstPdf.Width * firstScale;
            var firstHeight = firstPdf.Height * firstScale;

            if (firstWidth > PdfConstants.A4Width || firstHeight > PdfConstants.A4Height)
                continue;

            var remainingHeight = PdfConstants.A4Height - firstHeight - PdfConstants.Spacing;

            for (double secondScale = PdfConstants.ScaleMax;
                 secondScale >= PdfConstants.ScaleMin;
                 secondScale -= PdfConstants.ScaleStep)
            {
                var secondWidth = secondPdf.Width * secondScale;
                var secondHeight = secondPdf.Height * secondScale;

                if (secondWidth > PdfConstants.A4Width || secondHeight > remainingHeight)
                    continue;

                return ScaleCalculationResult.Success(firstScale, secondScale);
            }
        }

        return ScaleCalculationResult.Failure(BuildFailureReason(firstPdf, secondPdf));
    }

    private static string BuildFailureReason(PdfMetadata firstPdf, PdfMetadata secondPdf)
    {
        var minimum = PdfConstants.ScaleMin;
        var firstWidth = firstPdf.Width * minimum;
        var firstHeight = firstPdf.Height * minimum;
        var secondWidth = secondPdf.Width * minimum;
        var secondHeight = secondPdf.Height * minimum;
        var combinedHeight = firstHeight + PdfConstants.Spacing + secondHeight;
        var reasons = new List<string>();

        if (firstWidth > PdfConstants.A4Width)
            reasons.Add($"文件 A 缩小到 70% 后宽度 {firstWidth:F1} 磅，仍超过 A4 宽度 {PdfConstants.A4Width:F1} 磅");
        if (firstHeight > PdfConstants.A4Height)
            reasons.Add($"文件 A 缩小到 70% 后高度 {firstHeight:F1} 磅，仍超过 A4 高度 {PdfConstants.A4Height:F1} 磅");
        if (secondWidth > PdfConstants.A4Width)
            reasons.Add($"文件 B 缩小到 70% 后宽度 {secondWidth:F1} 磅，仍超过 A4 宽度 {PdfConstants.A4Width:F1} 磅");
        if (combinedHeight > PdfConstants.A4Height)
            reasons.Add($"两张发票均缩小到 70% 后的总高度（含间距）为 {combinedHeight:F1} 磅，超过 A4 高度 {PdfConstants.A4Height:F1} 磅");

        return reasons.Count > 0
            ? string.Join("；", reasons)
            : "在 70%～100% 允许缩放范围内未找到可放入 A4 的组合";
    }
}
