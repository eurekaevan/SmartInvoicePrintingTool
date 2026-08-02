using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
// 使用别名避开命名冲突
using MyPdfMetadata = SmartInvoicePrintingTool.Models.PdfMetadata;

namespace SmartInvoicePrintingTool.Services.Implementations;

public sealed class PdfMetadataService : IPdfMetadataService
{
    private readonly ILogger<PdfMetadataService> _logger;

    public PdfMetadataService(ILogger<PdfMetadataService> logger) => _logger = logger;

    public async Task<PdfMetadataReadResult> GetMetadataAsync(
        string pdfPath, CancellationToken ct = default)
    {
        if (!System.IO.File.Exists(pdfPath))
        {
            _logger.LogWarning("PDF 文件不存在: {Path}", pdfPath);
            return PdfMetadataReadResult.Failure("文件不存在");
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
                    return PdfMetadataReadResult.Failure("PDF 不包含任何页面");
                }

                var page = document.Pages[0];
                var width = page.Width.Point;
                var height = page.Height.Point;
                if (width <= 0 || height <= 0)
                    return PdfMetadataReadResult.Failure(
                        $"首页尺寸无效（宽 {width:F1}，高 {height:F1} 磅）");

                return PdfMetadataReadResult.Success(new MyPdfMetadata
                {
                    Path = pdfPath,
                    Width = width,
                    Height = height
                });
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
            return PdfMetadataReadResult.Failure(ex.Message);
        }
        finally
        {
            document?.Close();
        }
    }
}
