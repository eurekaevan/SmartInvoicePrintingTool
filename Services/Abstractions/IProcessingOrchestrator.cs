using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartInvoicePrintingTool.Models;

namespace SmartInvoicePrintingTool.Services.Abstractions;

public interface IProcessingOrchestrator
{
    Task<ProcessingResult> MergeAsync(
        string sourceFolder, string outputFolder,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    Task<PrintResult> PrintAsync(
        IReadOnlyList<string> pdfPaths, string printerName,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}
