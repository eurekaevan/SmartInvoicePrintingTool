using System.Threading;
using System.Threading.Tasks;
using InvoicePress.Models;

namespace InvoicePress.Services.Abstractions;

public interface IPdfMergingService
{
    Task<PdfMergeResult> MergeAsync(
        string pdf1Path, double scale1,
        string pdf2Path, double scale2,
        string outputPath, CancellationToken ct = default);

    Task<PdfMergeResult> CreateStandaloneAsync(
        string pdfPath, double scale,
        string outputPath, CancellationToken ct = default);
}
