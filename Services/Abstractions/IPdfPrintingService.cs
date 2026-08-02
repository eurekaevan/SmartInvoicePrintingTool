using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartInvoicePrintingTool.Models;

namespace SmartInvoicePrintingTool.Services.Abstractions;

public interface IPdfPrintingService
{
    Task<IReadOnlyList<string>> GetAvailablePrintersAsync();
    Task<PdfPrintSubmissionResult> PrintAsync(
        string pdfPath, string printerName, CancellationToken ct = default);
}
