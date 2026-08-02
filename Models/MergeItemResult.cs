namespace SmartInvoicePrintingTool.Models;

public sealed record MergeItemResult(
    string FirstFileName,
    string SecondFileName,
    string OutputFileName,
    string OutputPath,
    bool IsSuccess,
    string? ErrorMessage = null)
{
    public string StatusText => IsSuccess ? "已合并" : "合并失败";
}
