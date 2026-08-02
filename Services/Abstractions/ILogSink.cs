using System;

namespace InvoicePress.Services.Abstractions;

public interface ILogSink
{
    void Log(string message);
    event EventHandler<string>? LogMessage;
}