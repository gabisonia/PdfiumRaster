namespace PdfiumRaster;

/// <summary>
/// Configures PDF page rendering and image output.
/// </summary>
public sealed class PdfImageConversionOptions
{
    private byte _blackAndWhiteThreshold = 128;

    /// <summary>
    /// Gets or sets render settings such as DPI, size, rotation, and PDFium render flags.
    /// </summary>
    public PdfPageRenderOptions Render { get; set; } = new();

    /// <summary>
    /// Gets or sets the image format used by save helpers.
    /// </summary>
    public PdfImageOutputFormat Format { get; set; } = PdfImageOutputFormat.Bmp;

    /// <summary>
    /// Gets or sets compressed image encoding settings used by PNG, JPEG, and WebP save helpers.
    /// </summary>
    public PdfImageEncodingOptions Encoding { get; set; } = new();

    /// <summary>
    /// Gets or sets the color conversion applied after rendering.
    /// </summary>
    public PdfImageColorMode ColorMode { get; set; }

    /// <summary>
    /// Gets or sets the luminance threshold used for black-and-white conversion.
    /// </summary>
    public byte BlackAndWhiteThreshold
    {
        get => _blackAndWhiteThreshold;
        set => _blackAndWhiteThreshold = value;
    }
}
