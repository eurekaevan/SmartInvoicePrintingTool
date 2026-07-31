using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SmartInvoicePrintingTool.Services.Abstractions;
// 使用别名避开命名冲突
using MyPdfMetadata = SmartInvoicePrintingTool.Models.PdfMetadata;

namespace SmartInvoicePrintingTool.Services.Implementations;

public sealed class PdfMetadataService : IPdfMetadataService
{
    private readonly ILogger<PdfMetadataService> _logger;

    public PdfMetadataService(ILogger<PdfMetadataService> logger) => _logger = logger;

    public async Task<MyPdfMetadata?> GetMetadataAsync(string pdfPath, CancellationToken ct = default)
    {
        if (!System.IO.File.Exists(pdfPath))
        {
            _logger.LogWarning("PDF 文件不存在: {Path}", pdfPath);
            return null;
        }

        PdfDocument? document = null;
        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
                if (document.PageCount == 0)
                {
                    _logger.LogWarning("PDF 无页面: {Path}", pdfPath);
                    return null;
                }

                var page = document.Pages[0];
                return new MyPdfMetadata
                {
                    Path = pdfPath,
                    Width = page.Width.Point,
                    Height = page.Height.Point
                };
            }, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("操作已取消: {Path}", pdfPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取 PDF 失败: {Path}", pdfPath);
            return null;
        }
        finally
        {
            document?.Close();
        }
    }
}
