using System.Threading;
using System.Threading.Tasks;
using InvoicePress.Models;

namespace InvoicePress.Services.Abstractions;

public interface IPdfMetadataService
{
    Task<PdfMetadataReadResult> GetMetadataAsync(
        string pdfPath, CancellationToken ct = default);
}
