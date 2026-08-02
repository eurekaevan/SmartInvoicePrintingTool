using System.Collections.Generic;
using SmartInvoicePrintingTool.Models;

namespace SmartInvoicePrintingTool.Services.Abstractions;

public interface IPdfPairMatchingService
{
    PdfPairingResult MatchPairs(IReadOnlyList<PdfMetadata> pdfs);
}
