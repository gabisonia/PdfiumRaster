namespace PdfiumRaster;

public sealed class PdfPageRenderOptions
{
    public const double DefaultDpi = 300;

    private double _dpi = DefaultDpi;
    private PdfPageRotation _rotation;
    private double _scale = 1;

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

    public PdfRenderFlags Flags { get; set; } = PdfRenderFlags.Annot | PdfRenderFlags.LcdText;

    public int? Width { get; set; }

    public int? Height { get; set; }

    public bool WithAspectRatio { get; set; }

    public PdfAntiAliasing AntiAliasing { get; set; } = PdfAntiAliasing.All;

    public uint BackgroundColor { get; set; } = 0xFFFFFFFF;

    public bool FillBackground { get; set; } = true;

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
