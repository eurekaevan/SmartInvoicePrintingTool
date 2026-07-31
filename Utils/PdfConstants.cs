namespace SmartInvoicePrintingTool.Utils;

public static class PdfConstants
{
    // A4 尺寸（磅）
    public const double A4Width = 595.0;
    public const double A4Height = 842.0;
    public const double Spacing = 10.0;    // 两个 PDF 之间的间距
    
    // 缩放范围
    public const double ScaleMin = 0.70;
    public const double ScaleMax = 1.00;
    public const double ScaleStep = 0.001;
    
    // 分类阈值
    public const double ClassificationThreshold = 0.75; // 宽长比
}
