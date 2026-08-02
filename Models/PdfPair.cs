namespace SmartInvoicePrintingTool.Models;

public record PdfPair
{
    public required PdfMetadata FirstPdf { get; init; }
    public required PdfMetadata SecondPdf { get; init; }
    public double FirstScale { get; set; }
    public double SecondScale { get; set; }
    public string OutputFileName => $"{FirstPdf.FileName}_{SecondPdf.FileName}.pdf";
}
