using SmartInvoicePrintingTool.Models;

namespace SmartInvoicePrintingTool.Services.Abstractions;

public interface IScaleCalculationService
{
    (double FirstScale, double SecondScale)? CalculateScales(
        PdfMetadata firstPdf, PdfMetadata secondPdf);
}
