namespace PdfiumRaster;

/// <summary>
/// Defines post-processing color conversion applied to rendered images.
/// </summary>
public enum PdfImageColorMode
{
    /// <summary>
    /// Keeps the rendered image in full color.
    /// </summary>
    Color,

    /// <summary>
    /// Converts the rendered image to grayscale.
    /// </summary>
    Grayscale,

    /// <summary>
    /// Converts the rendered image to black and white using a luminance threshold.
    /// </summary>
    BlackAndWhite,
}
