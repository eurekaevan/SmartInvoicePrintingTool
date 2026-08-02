namespace InvoicePress.Models;

public sealed record PrintResult(
    int Requested,
    int Submitted,
    int Failed)
{
    public bool HasFailures => Failed > 0;
}
