using System;
using System.Threading;
using System.Threading.Tasks;
using SmartInvoicePrintingTool.Models;

namespace SmartInvoicePrintingTool.Services.Abstractions;

public interface IProcessingOrchestrator
{
    Task<ProcessingResult> ProcessAsync(
        string sourceFolder, string outputFolder, string printerName,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}
