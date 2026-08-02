using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;
using SmartInvoicePrintingTool.Utils;

namespace SmartInvoicePrintingTool.Services.Implementations;

public sealed class PdfMergingService : IPdfMergingService
{
    private readonly ILogger<PdfMergingService> _logger;

    public PdfMergingService(ILogger<PdfMergingService> logger) => _logger = logger;

    public async Task<PdfMergeResult> MergeAsync(
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

                if (form1.Page == null)
                    return PdfMergeResult.Failure("文件 A 没有可绘制的 PDF 页面");
                if (form2.Page == null)
                    return PdfMergeResult.Failure("文件 B 没有可绘制的 PDF 页面");

                outputDocument = new PdfDocument();
                var page = outputDocument.AddPage();
                page.Width = XUnit.FromPoint(PdfConstants.A4Width);
                page.Height = XUnit.FromPoint(PdfConstants.A4Height);

                using var gfx = XGraphics.FromPdfPage(page);

                var width1 = form1.Page.Width.Point;
                var height1 = form1.Page.Height.Point;
                var scaledWidth1 = width1 * scale1;
                var scaledHeight1 = height1 * scale1;

                var width2 = form2.Page.Width.Point;
                var height2 = form2.Page.Height.Point;
                var scaledWidth2 = width2 * scale2;
                var scaledHeight2 = height2 * scale2;

                // 与 smart_printer.py 一致：长发票贴齐顶部，短发票贴齐底部，
                // 只做水平居中，不额外添加固定间距。
                var rect1 = new XRect(
                    (PdfConstants.A4Width - scaledWidth1) / 2,
                    0,
                    scaledWidth1,
                    scaledHeight1);
                gfx.DrawImage(form1, rect1);

                var rect2 = new XRect(
                    (PdfConstants.A4Width - scaledWidth2) / 2,
                    PdfConstants.A4Height - scaledHeight2,
                    scaledWidth2,
                    scaledHeight2);
                gfx.DrawImage(form2, rect2);

                outputDocument.Save(outputPath);
                _logger.LogDebug("合并成功: {OutputPath}", outputPath);
                return PdfMergeResult.Success();
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
            return PdfMergeResult.Failure(ex.Message);
        }
        finally
        {
            form2?.Dispose();
            form1?.Dispose();
            outputDocument?.Dispose();
        }
    }

    public async Task<PdfMergeResult> CreateStandaloneAsync(
        string pdfPath, double scale,
        string outputPath, CancellationToken ct = default)
    {
        PdfDocument outputDocument = null!;
        XPdfForm? form = null;

        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                form = XPdfForm.FromFile(pdfPath);
                if (form.Page == null)
                    return PdfMergeResult.Failure("文件没有可绘制的 PDF 页面");

                outputDocument = new PdfDocument();
                var page = outputDocument.AddPage();
                page.Width = XUnit.FromPoint(PdfConstants.A4Width);
                page.Height = XUnit.FromPoint(PdfConstants.A4Height);

                using var gfx = XGraphics.FromPdfPage(page);
                var scaledWidth = form.Page.Width.Point * scale;
                var scaledHeight = form.Page.Height.Point * scale;
                var rect = new XRect(
                    (PdfConstants.A4Width - scaledWidth) / 2,
                    0,
                    scaledWidth,
                    scaledHeight);
                gfx.DrawImage(form, rect);

                outputDocument.Save(outputPath);
                _logger.LogDebug("单独成页成功: {OutputPath}", outputPath);
                return PdfMergeResult.Success();
            }, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("单独成页操作已取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "单独成页失败: {Pdf}", pdfPath);
            return PdfMergeResult.Failure(ex.Message);
        }
        finally
        {
            form?.Dispose();
            outputDocument?.Dispose();
        }
    }
}
