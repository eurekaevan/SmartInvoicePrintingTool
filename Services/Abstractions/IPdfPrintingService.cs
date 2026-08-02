using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InvoicePress.Models;

namespace InvoicePress.Services.Abstractions;

public interface IPdfPrintingService
{
    Task<IReadOnlyList<string>> GetAvailablePrintersAsync();
    Task<PdfPrintSubmissionResult> PrintAsync(
        string pdfPath, string printerName, CancellationToken ct = default);
}
