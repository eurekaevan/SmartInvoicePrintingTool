using SmartInvoicePrintingTool.Models;

namespace SmartInvoicePrintingTool.Services.Abstractions;

public interface IScaleCalculationService
{
    ScaleCalculationResult CalculateScales(
        PdfMetadata firstPdf, PdfMetadata secondPdf);
}
