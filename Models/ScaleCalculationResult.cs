namespace SmartInvoicePrintingTool.Models;

public sealed record ScaleCalculationResult(
    double FirstScale,
    double SecondScale,
    string? ErrorMessage)
{
    public bool IsSuccess => ErrorMessage == null;

    public static ScaleCalculationResult Success(double firstScale, double secondScale) =>
        new(firstScale, secondScale, null);

    public static ScaleCalculationResult Failure(string errorMessage) =>
        new(0, 0, errorMessage);
}
