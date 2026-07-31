using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Utils;

namespace SmartInvoicePrintingTool.Services.Implementations;

public sealed class PdfMergingService : IPdfMergingService
{
    private readonly ILogger<PdfMergingService> _logger;

    public PdfMergingService(ILogger<PdfMergingService> logger) => _logger = logger;

    public async Task<bool> MergeAsync(
        string pdf1Path, double scale1,
        string pdf2Path, double scale2,
        string outputPath, CancellationToken ct = default)
    {
        PdfDocument outputDocument = null!;
        XPdfForm? form1 = null;
        XPdfForm? form2 = null;

        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                form1 = XPdfForm.FromFile(pdf1Path);
                form2 = XPdfForm.FromFile(pdf2Path);

                if (form1.Page == null || form2.Page == null) return false;

                outputDocument = new PdfDocument();
                var page = outputDocument.AddPage();
                page.Width = XUnit.FromPoint(PdfConstants.A4Width);
                page.Height = XUnit.FromPoint(PdfConstants.A4Height);

                using var gfx = XGraphics.FromPdfPage(page);

                // 绘制第一个 PDF
                var width1 = form1.Page.Width.Point;
                var height1 = form1.Page.Height.Point;
                var rect1 = new XRect(
                    0, 0,
                    width1 * scale1,
                    height1 * scale1);
                gfx.DrawImage(form1, rect1);

                // 绘制第二个 PDF
                var width2 = form2.Page.Width.Point;
                var height2 = form2.Page.Height.Point;
                var rect2 = new XRect(
                    0, height1 * scale1 + PdfConstants.Spacing,
                    width2 * scale2,
                    height2 * scale2);
                gfx.DrawImage(form2, rect2);

                outputDocument.Save(outputPath);
                _logger.LogDebug("合并成功: {OutputPath}", outputPath);
                return true;
            }, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("合并操作已取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "合并失败: {Pdf1} + {Pdf2}", pdf1Path, pdf2Path);
            return false;
        }
        finally
        {
            form2?.Dispose();
            form1?.Dispose();
            outputDocument?.Dispose();
        }
    }
}
