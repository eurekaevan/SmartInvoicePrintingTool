namespace SmartInvoicePrintingTool.Models;

public sealed record ProcessingResult(
    int InputCount,
    int ValidPdfCount,
    int PairCount,
    int UnpairedCount,
    int MergeSucceeded,
    int MergeFailed,
    int PrintSubmitted,
    int PrintFailed)
{
    public bool HasFailures => MergeFailed > 0 || PrintFailed > 0;
}
