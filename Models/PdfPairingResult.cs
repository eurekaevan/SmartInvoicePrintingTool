using System.Collections.Generic;

namespace SmartInvoicePrintingTool.Models;

public sealed record PdfPairingResult(
    IReadOnlyList<PdfPair> Pairs,
    PdfMetadata? StandalonePdf);
