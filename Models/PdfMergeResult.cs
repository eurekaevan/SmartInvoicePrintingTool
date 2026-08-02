namespace InvoicePress.Models;

public sealed record PdfMergeResult(bool IsSuccess, string? ErrorMessage)
{
    public static PdfMergeResult Success() => new(true, null);

    public static PdfMergeResult Failure(string errorMessage) => new(false, errorMessage);
}
