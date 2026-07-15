namespace PdfiumRaster;

/// <summary>
/// Configures compressed image encoding.
/// </summary>
public sealed class PdfImageEncodingOptions
{
    private int _quality = 100;
    private int? _pngCompressionLevel;

    /// <summary>
    /// Gets encoding settings intended for fast preview and batch output.
    /// </summary>
    /// <remarks>
    /// A new options instance is returned on every access. JPEG and WebP quality is 85 and PNG compression is 1.
    /// </remarks>
    public static PdfImageEncodingOptions Fast => new()
    {
        Quality = 85,
        PngCompressionLevel = 1,
    };

    /// <summary>
    /// Gets or sets JPEG and lossy WebP quality from 0 to 100. The default is 100.
    /// </summary>
    public int Quality
    {
        get => _quality;
        set
        {
            if (value < 0 || value > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Quality must be from 0 to 100.");
            }

            _quality = value;
        }
    }

    /// <summary>
    /// Gets or sets the PNG zlib compression level from 0 to 9, or <see langword="null" /> to use the Skia default.
    /// </summary>
    public int? PngCompressionLevel
    {
        get => _pngCompressionLevel;
        set
        {
            if (value is < 0 or > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "PNG compression level must be from 0 to 9 or null.");
            }

            _pngCompressionLevel = value;
        }
    }
}
