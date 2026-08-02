namespace InvoicePress.Models;

public sealed record StandalonePdfPlan(
    PdfMetadata Pdf,
    string Reason);
