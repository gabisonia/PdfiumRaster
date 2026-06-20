namespace PdfiumRaster;

/// <summary>
/// Supported image file formats for saved rendered pages.
/// </summary>
public enum PdfImageOutputFormat
{
    /// <summary>
    /// Windows bitmap output.
    /// </summary>
    Bmp,

    /// <summary>
    /// Portable Network Graphics output.
    /// </summary>
    Png,

    /// <summary>
    /// JPEG output.
    /// </summary>
    Jpeg,

    /// <summary>
    /// WebP output.
    /// </summary>
    Webp,
}
