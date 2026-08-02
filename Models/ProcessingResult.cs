using System.Collections.Generic;

namespace SmartInvoicePrintingTool.Models;

public sealed record ProcessingResult(
    int InputCount,
    int ValidPdfCount,
    int PairCount,
    int UnpairedCount,
    int MergeSucceeded,
    int MergeFailed,
    IReadOnlyList<MergeItemResult> PairResults)
{
    public bool HasFailures => MergeFailed > 0;
}
