namespace InvoicePress.Models;

public sealed record StandalonePdfResult(
    string SourceFileName,
    string OutputFileName,
    string OutputPath,
    bool IsSuccess,
    string Reason,
    string? ErrorMessage = null)
{
    public string StatusText => IsSuccess ? "单独成页" : "处理失败";
}
