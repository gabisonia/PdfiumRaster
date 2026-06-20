namespace PdfiumRaster;

/// <summary>
/// Represents a PDF page size in points.
/// </summary>
public readonly struct PdfPageSize
{
    /// <summary>
    /// Initializes a page size.
    /// </summary>
    /// <param name="width">Page width in PDF points.</param>
    /// <param name="height">Page height in PDF points.</param>
    public PdfPageSize(double width, double height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets the page width in PDF points.
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// Gets the page height in PDF points.
    /// </summary>
    public double Height { get; }
}
