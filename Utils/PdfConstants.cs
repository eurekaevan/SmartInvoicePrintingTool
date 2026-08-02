namespace InvoicePress.Utils;

public static class PdfConstants
{
    // A4 尺寸（磅）
    public const double A4Width = 595.0;
    public const double A4Height = 842.0;

    // 缩放范围（每次递减 1 个百分点）
    public const int ScaleMinPercent = 70;
    public const int ScaleMaxPercent = 100;
    public const double StandaloneScale = 0.70;
}
