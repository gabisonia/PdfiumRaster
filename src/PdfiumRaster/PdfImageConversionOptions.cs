namespace PdfiumRaster;

public sealed class PdfImageConversionOptions
{
    private byte _blackAndWhiteThreshold = 128;

    public PdfPageRenderOptions Render { get; set; } = new();

    public PdfImageOutputFormat Format { get; set; } = PdfImageOutputFormat.Bmp;

    public PdfImageColorMode ColorMode { get; set; }

    public byte BlackAndWhiteThreshold
    {
        get => _blackAndWhiteThreshold;
        set => _blackAndWhiteThreshold = value;
    }
}
