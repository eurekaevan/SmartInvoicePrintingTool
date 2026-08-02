using System;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Utils;

namespace SmartInvoicePrintingTool.Services.Implementations;

public class ScaleCalculationService : IScaleCalculationService
{
    public (double FirstScale, double SecondScale)? CalculateScales(
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

                return (firstScale, secondScale);
            }
        }

        return null;
    }
}
