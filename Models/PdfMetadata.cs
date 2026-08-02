namespace InvoicePress.Models;

public record PdfMetadata
{
    public required string Path { get; init; }
    public double Width { get; init; }   // 磅
    public double Height { get; init; }  // 磅
    public string FileName => System.IO.Path.GetFileNameWithoutExtension(Path);
}