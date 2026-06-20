namespace PdfiumRaster;

public readonly struct PdfPageSize
{
    public PdfPageSize(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }
}
