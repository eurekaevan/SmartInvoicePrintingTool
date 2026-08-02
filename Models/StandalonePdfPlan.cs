namespace SmartInvoicePrintingTool.Models;

public sealed record StandalonePdfPlan(
    PdfMetadata Pdf,
    string Reason);
