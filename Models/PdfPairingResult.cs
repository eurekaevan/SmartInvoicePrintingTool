using System.Collections.Generic;

namespace InvoicePress.Models;

public sealed record PdfPairingResult(
    IReadOnlyList<PdfPair> Pairs,
    IReadOnlyList<StandalonePdfPlan> StandalonePdfs);
