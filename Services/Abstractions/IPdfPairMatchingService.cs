using System.Collections.Generic;
using InvoicePress.Models;

namespace InvoicePress.Services.Abstractions;

public interface IPdfPairMatchingService
{
    PdfPairingResult MatchPairs(IReadOnlyList<PdfMetadata> pdfs);
}
