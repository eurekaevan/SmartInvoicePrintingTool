namespace SmartInvoicePrintingTool.Models;

public sealed record PdfMetadataReadResult(
    PdfMetadata? Metadata,
    string? ErrorMessage)
{
    public bool IsSuccess => Metadata != null;

    public static PdfMetadataReadResult Success(PdfMetadata metadata) => new(metadata, null);

    public static PdfMetadataReadResult Failure(string errorMessage) => new(null, errorMessage);
}
