using System;
using Microsoft.Extensions.Logging;
using InvoicePress.Services.Abstractions;

namespace InvoicePress.Services.Implementations;

public class ReactiveLogSink : ILogSink
{
    private readonly ILogger<ReactiveLogSink> _logger;
    public event EventHandler<string>? LogMessage;

    public ReactiveLogSink(ILogger<ReactiveLogSink> logger) => _logger = logger;

    public void Log(string message)
    {
        _logger.LogInformation(message);
        LogMessage?.Invoke(this, $"{DateTime.Now:HH:mm:ss} {message}");
    }
}