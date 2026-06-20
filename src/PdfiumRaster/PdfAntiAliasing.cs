namespace PdfiumRaster;

/// <summary>
/// Controls smoothing applied while rendering PDF page content.
/// </summary>
[Flags]
public enum PdfAntiAliasing
{
    /// <summary>
    /// Disables smoothing for text, images, and vector paths.
    /// </summary>
    None = 0,

    /// <summary>
    /// Enables text smoothing.
    /// </summary>
    Text = 1 << 0,

    /// <summary>
    /// Enables image smoothing.
    /// </summary>
    Images = 1 << 1,

    /// <summary>
    /// Enables vector path smoothing.
    /// </summary>
    Paths = 1 << 2,

    /// <summary>
    /// Enables all smoothing options.
    /// </summary>
    All = Text | Images | Paths,
}
