namespace InvoicePress.Models;

public sealed record PdfPrintSubmissionResult(bool IsSuccess, string? ErrorMessage)
{
    public static PdfPrintSubmissionResult Success() => new(true, null);

    public static PdfPrintSubmissionResult Failure(string errorMessage) => new(false, errorMessage);
}
