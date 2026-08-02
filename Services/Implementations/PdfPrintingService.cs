using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartInvoicePrintingTool.Models;
using SmartInvoicePrintingTool.Services.Abstractions;

namespace SmartInvoicePrintingTool.Services.Implementations;

/// <summary>
/// 使用 Windows 系统默认 PDF 打印服务。
/// 注意：此方式依赖于用户安装的 PDF 阅读器（如 Edge、Adobe Reader）。
/// </summary>
public sealed class PdfPrintingService : IPdfPrintingService
{
    private readonly ILogger<PdfPrintingService> _logger;

    public PdfPrintingService(ILogger<PdfPrintingService> logger) => _logger = logger;

    public Task<IReadOnlyList<string>> GetAvailablePrintersAsync()
    {
        // 注意：不使用 System.Drawing.Printing 依赖，改用 Win32 API 或 WMI 获取可能更好，
        // 但既然保留 System.Drawing，此处保留原引用
        try
        {
            return Task.FromResult<IReadOnlyList<string>>(
                System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>().ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取打印机列表失败");
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }

    public Task<PdfPrintSubmissionResult> PrintAsync(
        string pdfPath, string printerName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(pdfPath))
        {
            _logger.LogError("待打印 PDF 不存在: {File}", pdfPath);
            return Task.FromResult(PdfPrintSubmissionResult.Failure("待打印 PDF 文件不存在"));
        }

        if (string.IsNullOrWhiteSpace(printerName))
        {
            _logger.LogError("未指定打印机: {File}", pdfPath);
            return Task.FromResult(PdfPrintSubmissionResult.Failure("未指定打印机"));
        }

        try
        {
            // printto 会把目标打印机传给系统关联的 PDF 阅读器。
            // 成功仅表示任务已交给关联程序，不代表物理打印已经完成。
            var psi = new ProcessStartInfo
            {
                FileName = pdfPath,
                Verb = "printto",
                CreateNoWindow = true,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            psi.ArgumentList.Add(printerName);

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError("无法启动打印进程: {File}", pdfPath);
                return Task.FromResult(PdfPrintSubmissionResult.Failure("系统未能启动 PDF 关联打印程序"));
            }

            _logger.LogInformation(
                "已将打印任务提交给关联程序: {File} -> {Printer}",
                Path.GetFileName(pdfPath),
                printerName);
            return Task.FromResult(PdfPrintSubmissionResult.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打印任务提交失败: {File} -> {Printer}", pdfPath, printerName);
            return Task.FromResult(PdfPrintSubmissionResult.Failure(ex.Message));
        }
    }
}
