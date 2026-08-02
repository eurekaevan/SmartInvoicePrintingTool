using System.Collections.Generic;
using System.Linq;

namespace SmartInvoicePrintingTool.Models;

public sealed record ProcessingResult(
    int InputCount,
    int ValidPdfCount,
    int PairCount,
    int MergeSucceeded,
    int MergeFailed,
    IReadOnlyList<MergeItemResult> PairResults,
    IReadOnlyList<StandalonePdfResult> StandaloneResults)
{
    public bool HasFailures =>
        MergeFailed > 0 || StandaloneResults.Any(item => !item.IsSuccess);
}
