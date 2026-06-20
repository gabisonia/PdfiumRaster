namespace PdfiumRaster;

public static class PdfImageConverter
{
    public static int GetPageCount(string pdfPath, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);

        return document.PageCount;
    }

    public static int GetPageCount(byte[] pdfBytes, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfBytes, password);

        return document.PageCount;
    }

    public static int GetPageCount(Stream pdfStream, bool leaveOpen = false, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfStream, leaveOpen, password);

        return document.PageCount;
    }

    public static int GetPageCountFromBase64(string pdfBase64, string? password = null)
    {
        if (pdfBase64 is null)
        {
            throw new ArgumentNullException(nameof(pdfBase64));
        }

        return GetPageCount(Convert.FromBase64String(pdfBase64), password);
    }

    public static IReadOnlyList<PdfPageSize> GetPageSizes(string pdfPath, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);

        return GetPageSizes(document);
    }

    public static IReadOnlyList<PdfPageSize> GetPageSizes(byte[] pdfBytes, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfBytes, password);

        return GetPageSizes(document);
    }

    public static IReadOnlyList<PdfPageSize> GetPageSizes(Stream pdfStream, bool leaveOpen = false, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfStream, leaveOpen, password);

        return GetPageSizes(document);
    }

    public static IReadOnlyList<PdfPageSize> GetPageSizesFromBase64(string pdfBase64, string? password = null)
    {
        if (pdfBase64 is null)
        {
            throw new ArgumentNullException(nameof(pdfBase64));
        }

        return GetPageSizes(Convert.FromBase64String(pdfBase64), password);
    }

    public static PdfBitmap RenderPage(
        string pdfPath,
        int pageIndex,
        PdfPageRenderOptions? options = null,
        string? password = null)
    {
        return RenderPage(pdfPath, pageIndex, new PdfImageConversionOptions
        {
            Render = options ?? new PdfPageRenderOptions(),
        }, password);
    }

    public static PdfBitmap RenderPage(
        string pdfPath,
        int pageIndex,
        PdfImageConversionOptions? options,
        string? password = null)
    {
        options ??= new PdfImageConversionOptions();

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);
        using var page = document.LoadPage(pageIndex);

        var bitmap = page.Render(GetRenderOptions(options));
        ApplyColorMode(bitmap, options);

        return bitmap;
    }

    public static PdfBitmap RenderPage(
        byte[] pdfBytes,
        int pageIndex,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        options ??= new PdfImageConversionOptions();

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfBytes, password);
        using var page = document.LoadPage(pageIndex);

        var bitmap = page.Render(GetRenderOptions(options));
        ApplyColorMode(bitmap, options);

        return bitmap;
    }

    public static PdfBitmap RenderPage(
        Stream pdfStream,
        int pageIndex,
        PdfImageConversionOptions? options = null,
        bool leaveOpen = false,
        string? password = null)
    {
        options ??= new PdfImageConversionOptions();

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfStream, leaveOpen, password);
        using var page = document.LoadPage(pageIndex);

        var bitmap = page.Render(GetRenderOptions(options));
        ApplyColorMode(bitmap, options);

        return bitmap;
    }

    public static PdfBitmap RenderPageFromBase64(
        string pdfBase64,
        int pageIndex,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        if (pdfBase64 is null)
        {
            throw new ArgumentNullException(nameof(pdfBase64));
        }

        return RenderPage(Convert.FromBase64String(pdfBase64), pageIndex, options, password);
    }

    public static PdfBitmap RenderPageNumber(
        string pdfPath,
        int pageNumber,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        return RenderPage(pdfPath, ToPageIndex(pageNumber), options, password);
    }

    public static PdfBitmap RenderPageNumber(
        byte[] pdfBytes,
        int pageNumber,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        return RenderPage(pdfBytes, ToPageIndex(pageNumber), options, password);
    }

    public static void SavePage(
        string pdfPath,
        int pageIndex,
        string imagePath,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path cannot be null or whitespace.", nameof(imagePath));
        }

        options ??= new PdfImageConversionOptions();
        var bitmap = RenderPage(pdfPath, pageIndex, options, password);

        SaveBitmap(bitmap, imagePath, options.Format);
    }

    public static void SavePageNumber(
        string pdfPath,
        int pageNumber,
        string imagePath,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        SavePage(pdfPath, ToPageIndex(pageNumber), imagePath, options, password);
    }

    public static void SavePng(string pdfPath, int pageNumber, string imagePath, PdfImageConversionOptions? options = null, string? password = null)
    {
        SavePageNumber(pdfPath, pageNumber, imagePath, WithFormat(options, PdfImageOutputFormat.Png), password);
    }

    public static void SaveJpeg(string pdfPath, int pageNumber, string imagePath, PdfImageConversionOptions? options = null, string? password = null)
    {
        SavePageNumber(pdfPath, pageNumber, imagePath, WithFormat(options, PdfImageOutputFormat.Jpeg), password);
    }

    public static void SaveWebp(string pdfPath, int pageNumber, string imagePath, PdfImageConversionOptions? options = null, string? password = null)
    {
        SavePageNumber(pdfPath, pageNumber, imagePath, WithFormat(options, PdfImageOutputFormat.Webp), password);
    }

    public static IEnumerable<PdfBitmap> RenderDocument(
        string pdfPath,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);

        foreach (var bitmap in RenderPages(document, null, options))
        {
            yield return bitmap;
        }
    }

    public static IEnumerable<PdfBitmap> RenderPages(
        string pdfPath,
        IEnumerable<int> pageIndexes,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        if (pageIndexes is null)
        {
            throw new ArgumentNullException(nameof(pageIndexes));
        }

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);

        foreach (var bitmap in RenderPages(document, pageIndexes, options))
        {
            yield return bitmap;
        }
    }

    public static int SaveDocument(
        string pdfPath,
        string outputDirectory,
        string fileNamePrefix = "page",
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or whitespace.", nameof(outputDirectory));
        }

        if (string.IsNullOrWhiteSpace(fileNamePrefix))
        {
            throw new ArgumentException("File name prefix cannot be null or whitespace.", nameof(fileNamePrefix));
        }

        Directory.CreateDirectory(outputDirectory);

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);

        options ??= new PdfImageConversionOptions();
        var extension = GetExtension(options.Format);

        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            using var page = document.LoadPage(pageIndex);
            var bitmap = page.Render(GetRenderOptions(options));
            ApplyColorMode(bitmap, options);
            var imagePath = Path.Combine(outputDirectory, $"{fileNamePrefix}-{pageIndex + 1:D4}{extension}");

            SaveBitmap(bitmap, imagePath, options.Format);
        }

        return document.PageCount;
    }

    public static void SaveBitmap(PdfBitmap bitmap, string path, PdfImageOutputFormat format)
    {
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        switch (format)
        {
            case PdfImageOutputFormat.Bmp:
                PdfImageWriter.SaveBmp(bitmap, path);
                break;
            case PdfImageOutputFormat.Png:
                PdfImageWriter.SavePng(bitmap, path);
                break;
            case PdfImageOutputFormat.Jpeg:
                PdfImageWriter.SaveJpeg(bitmap, path);
                break;
            case PdfImageOutputFormat.Webp:
                PdfImageWriter.SaveWebp(bitmap, path);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Image format is not supported.");
        }
    }

    public static void ApplyColorMode(
        PdfBitmap bitmap,
        PdfImageColorMode colorMode,
        byte blackAndWhiteThreshold = 128)
    {
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        switch (colorMode)
        {
            case PdfImageColorMode.Color:
                return;
            case PdfImageColorMode.Grayscale:
                ApplyGrayscale(bitmap);
                return;
            case PdfImageColorMode.BlackAndWhite:
                ApplyBlackAndWhite(bitmap, blackAndWhiteThreshold);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, "Image color mode is not supported.");
        }
    }

    private static string GetExtension(PdfImageOutputFormat format)
    {
        return format switch
        {
            PdfImageOutputFormat.Bmp => ".bmp",
            PdfImageOutputFormat.Png => ".png",
            PdfImageOutputFormat.Jpeg => ".jpg",
            PdfImageOutputFormat.Webp => ".webp",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Image format is not supported."),
        };
    }

    private static PdfPageRenderOptions GetRenderOptions(PdfImageConversionOptions options)
    {
        var source = options.Render ?? new PdfPageRenderOptions();
        var renderOptions = new PdfPageRenderOptions
        {
            Dpi = source.Dpi,
            Scale = source.Scale,
            Rotation = source.Rotation,
            Flags = source.Flags,
            BackgroundColor = source.BackgroundColor,
            FillBackground = source.FillBackground,
            Width = source.Width,
            Height = source.Height,
            WithAspectRatio = source.WithAspectRatio,
            AntiAliasing = source.AntiAliasing,
        };

        if (options.ColorMode is PdfImageColorMode.Grayscale or PdfImageColorMode.BlackAndWhite)
        {
            renderOptions.Flags |= PdfRenderFlags.Grayscale;
        }

        return renderOptions;
    }

    private static void ApplyColorMode(PdfBitmap bitmap, PdfImageConversionOptions options)
    {
        ApplyColorMode(bitmap, options.ColorMode, options.BlackAndWhiteThreshold);
    }

    private static void ApplyGrayscale(PdfBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowOffset = y * bitmap.Stride;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var offset = rowOffset + x * 4;
                var gray = GetLuminance(bitmap.Pixels[offset + 2], bitmap.Pixels[offset + 1], bitmap.Pixels[offset]);

                bitmap.Pixels[offset] = gray;
                bitmap.Pixels[offset + 1] = gray;
                bitmap.Pixels[offset + 2] = gray;
            }
        }
    }

    private static void ApplyBlackAndWhite(PdfBitmap bitmap, byte threshold)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowOffset = y * bitmap.Stride;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var offset = rowOffset + x * 4;
                var gray = GetLuminance(bitmap.Pixels[offset + 2], bitmap.Pixels[offset + 1], bitmap.Pixels[offset]);
                var value = gray >= threshold ? byte.MaxValue : byte.MinValue;

                bitmap.Pixels[offset] = value;
                bitmap.Pixels[offset + 1] = value;
                bitmap.Pixels[offset + 2] = value;
            }
        }
    }

    private static byte GetLuminance(byte red, byte green, byte blue)
    {
        return (byte)((red * 299 + green * 587 + blue * 114 + 500) / 1000);
    }

    private static int ToPageIndex(int pageNumber)
    {
        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Page number is 1-based and must be greater than zero.");
        }

        return pageNumber - 1;
    }

    private static IReadOnlyList<PdfPageSize> GetPageSizes(PdfDocument document)
    {
        var sizes = new List<PdfPageSize>(document.PageCount);

        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            using var page = document.LoadPage(pageIndex);
            sizes.Add(new PdfPageSize(page.Width, page.Height));
        }

        return sizes;
    }

    private static IEnumerable<PdfBitmap> RenderPages(
        PdfDocument document,
        IEnumerable<int>? pageIndexes,
        PdfImageConversionOptions? options)
    {
        options ??= new PdfImageConversionOptions();
        var indexes = pageIndexes ?? Enumerable.Range(0, document.PageCount);

        foreach (var pageIndex in indexes.OrderBy(page => page).Distinct())
        {
            using var page = document.LoadPage(pageIndex);
            var bitmap = page.Render(GetRenderOptions(options));
            ApplyColorMode(bitmap, options);

            yield return bitmap;
        }
    }

    private static PdfImageConversionOptions WithFormat(PdfImageConversionOptions? options, PdfImageOutputFormat format)
    {
        options ??= new PdfImageConversionOptions();
        options.Format = format;
        return options;
    }
}
