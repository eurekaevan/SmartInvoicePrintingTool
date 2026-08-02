using System.Threading;
using System.Threading.Tasks;
using SmartInvoicePrintingTool.Models;

namespace SmartInvoicePrintingTool.Services.Abstractions;

public interface IPdfMetadataService
{
    Task<PdfMetadataReadResult> GetMetadataAsync(
        string pdfPath, CancellationToken ct = default);
}
