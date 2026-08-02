using InvoicePress.Models;

namespace InvoicePress.Services.Abstractions;

public interface IScaleCalculationService
{
    ScaleCalculationResult CalculateScales(
        PdfMetadata firstPdf, PdfMetadata secondPdf);
}
