using System.Collections.Generic;

namespace SmartInvoicePrintingTool.Models;

public sealed record ProcessingResult(
    int InputCount,
    int ValidPdfCount,
    int PairCount,
    int MergeSucceeded,
    int MergeFailed,
    IReadOnlyList<MergeItemResult> PairResults,
    StandalonePdfResult? StandalonePdf)
{
    public bool HasFailures => MergeFailed > 0;
}
