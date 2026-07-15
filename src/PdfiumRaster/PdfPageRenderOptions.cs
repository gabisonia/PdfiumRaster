namespace PdfiumRaster;

/// <summary>
/// Configures how a PDF page is rasterized.
/// </summary>
public sealed class PdfPageRenderOptions
{
    /// <summary>
    /// Default render resolution in dots per inch.
    /// </summary>
    public const double DefaultDpi = 300;

    /// <summary>
    /// Gets render settings intended for screen previews at 96 DPI.
    /// </summary>
    /// <remarks>A new options instance is returned on every access.</remarks>
    public static PdfPageRenderOptions ScreenPreview => new() { Dpi = 96 };

    private double _dpi = DefaultDpi;
    private PdfPageRotation _rotation;
    private double _scale = 1;

    /// <summary>
    /// Gets or sets the render resolution in dots per inch.
    /// </summary>
    public double Dpi
    {
        get => _dpi;
        set
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "DPI must be a finite value greater than zero.");
            }

            _dpi = value;
        }
    }

    /// <summary>
    /// Gets or sets an additional scale multiplier applied after DPI conversion.
    /// </summary>
    public double Scale
    {
        get => _scale;
        set
        {
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Scale must be a finite value greater than zero.");
            }

            _scale = value;
        }
    }

    /// <summary>
    /// Gets or sets page rotation applied while rendering.
    /// </summary>
    public PdfPageRotation Rotation
    {
        get => _rotation;
        set
        {
            if (!Enum.IsDefined(typeof(PdfPageRotation), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Rotation must be a defined page rotation.");
            }

            _rotation = value;
        }
    }

    /// <summary>
    /// Gets or sets PDFium render flags.
    /// </summary>
    public PdfRenderFlags Flags { get; set; } = PdfRenderFlags.Annot | PdfRenderFlags.LcdText;

    /// <summary>
    /// Gets or sets an explicit output width in pixels.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets an explicit output height in pixels.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets whether the missing dimension should be calculated from the page aspect ratio.
    /// </summary>
    public bool WithAspectRatio { get; set; }

    /// <summary>
    /// Gets or sets anti-aliasing options.
    /// </summary>
    public PdfAntiAliasing AntiAliasing { get; set; } = PdfAntiAliasing.All;

    /// <summary>
    /// Gets or sets the ARGB background color used before rendering.
    /// </summary>
    public uint BackgroundColor { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Gets or sets whether the bitmap background is filled before rendering the page.
    /// </summary>
    public bool FillBackground { get; set; } = true;

    /// <summary>
    /// Calculates the output pixel size for a page size in PDF points.
    /// </summary>
    /// <param name="pageWidthPoints">Page width in PDF points.</param>
    /// <param name="pageHeightPoints">Page height in PDF points.</param>
    /// <returns>The output width and height in pixels.</returns>
    public (int Width, int Height) GetPixelSize(double pageWidthPoints, double pageHeightPoints)
    {
        if (pageWidthPoints <= 0 || double.IsNaN(pageWidthPoints) || double.IsInfinity(pageWidthPoints))
        {
            throw new ArgumentOutOfRangeException(nameof(pageWidthPoints), pageWidthPoints,
                "Page width must be finite and greater than zero.");
        }

        if (pageHeightPoints <= 0 || double.IsNaN(pageHeightPoints) || double.IsInfinity(pageHeightPoints))
        {
            throw new ArgumentOutOfRangeException(nameof(pageHeightPoints), pageHeightPoints,
                "Page height must be finite and greater than zero.");
        }

        var width = Width ?? ToPixels(pageWidthPoints);
        var height = Height ?? ToPixels(pageHeightPoints);

        if (WithAspectRatio)
        {
            if (Width.HasValue == Height.HasValue)
            {
                throw new InvalidOperationException("Exactly one of Width or Height must be set when WithAspectRatio is true.");
            }

            var aspectRatio = pageWidthPoints / pageHeightPoints;
            if (Width.HasValue)
            {
                height = Math.Max(1, checked((int)Math.Round(width / aspectRatio)));
            }
            else
            {
                width = Math.Max(1, checked((int)Math.Round(height * aspectRatio)));
            }
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), width, "Rendered width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Height), height, "Rendered height must be greater than zero.");
        }

        return Rotation is PdfPageRotation.Rotate90 or PdfPageRotation.Rotate270
            ? (height, width)
            : (width, height);
    }

    internal PdfRenderFlags GetRenderFlags()
    {
        var flags = Flags;

        if (!AntiAliasing.HasFlag(PdfAntiAliasing.Text))
        {
            flags |= PdfRenderFlags.RenderNoSmoothText;
        }

        if (!AntiAliasing.HasFlag(PdfAntiAliasing.Images))
        {
            flags |= PdfRenderFlags.RenderNoSmoothImage;
        }

        if (!AntiAliasing.HasFlag(PdfAntiAliasing.Paths))
        {
            flags |= PdfRenderFlags.RenderNoSmoothPath;
        }

        return flags;
    }

    private int ToPixels(double points)
    {
        return Math.Max(1, checked((int)Math.Ceiling(points / 72d * Dpi * Scale)));
    }
}
