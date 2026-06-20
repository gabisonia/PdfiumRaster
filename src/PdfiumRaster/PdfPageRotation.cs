namespace PdfiumRaster;

/// <summary>
/// Defines page rotation values passed to PDFium rendering.
/// </summary>
public enum PdfPageRotation
{
    /// <summary>
    /// No rotation.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Rotate the page 90 degrees clockwise.
    /// </summary>
    Rotate90 = 1,

    /// <summary>
    /// Rotate the page 180 degrees.
    /// </summary>
    Rotate180 = 2,

    /// <summary>
    /// Rotate the page 270 degrees clockwise.
    /// </summary>
    Rotate270 = 3,
}
