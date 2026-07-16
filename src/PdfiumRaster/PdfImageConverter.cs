namespace PdfiumRaster;

/// <summary>
/// Provides convenience methods for rendering PDF pages to image bitmaps and files.
/// </summary>
public static class PdfImageConverter
{
    /// <summary>
    /// Gets the page count for a PDF file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The number of pages in the document.</returns>
    public static int GetPageCount(string pdfPath, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);

        return document.PageCount;
    }

    /// <summary>
    /// Gets the page count for PDF bytes.
    /// </summary>
    /// <param name="pdfBytes">PDF file bytes.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The number of pages in the document.</returns>
    public static int GetPageCount(byte[] pdfBytes, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfBytes, password);

        return document.PageCount;
    }

    /// <summary>
    /// Gets the page count for a PDF stream.
    /// </summary>
    /// <param name="pdfStream">Stream containing PDF file data.</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="pdfStream"/> open after loading.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The number of pages in the document.</returns>
    public static int GetPageCount(Stream pdfStream, bool leaveOpen = false, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfStream, leaveOpen, password);

        return document.PageCount;
    }

    /// <summary>
    /// Gets the page count for a Base64-encoded PDF.
    /// </summary>
    /// <param name="pdfBase64">Base64-encoded PDF file data.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The number of pages in the document.</returns>
    public static int GetPageCountFromBase64(string pdfBase64, string? password = null)
    {
        if (pdfBase64 is null)
        {
            throw new ArgumentNullException(nameof(pdfBase64));
        }

        return GetPageCount(Convert.FromBase64String(pdfBase64), password);
    }

    /// <summary>
    /// Gets all page sizes from a PDF file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>Page sizes in PDF points.</returns>
    public static IReadOnlyList<PdfPageSize> GetPageSizes(string pdfPath, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);

        return GetPageSizes(document);
    }

    /// <summary>
    /// Gets all page sizes from PDF bytes.
    /// </summary>
    /// <param name="pdfBytes">PDF file bytes.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>Page sizes in PDF points.</returns>
    public static IReadOnlyList<PdfPageSize> GetPageSizes(byte[] pdfBytes, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfBytes, password);

        return GetPageSizes(document);
    }

    /// <summary>
    /// Gets all page sizes from a PDF stream.
    /// </summary>
    /// <param name="pdfStream">Stream containing PDF file data.</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="pdfStream"/> open after loading.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>Page sizes in PDF points.</returns>
    public static IReadOnlyList<PdfPageSize> GetPageSizes(Stream pdfStream, bool leaveOpen = false, string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfStream, leaveOpen, password);

        return GetPageSizes(document);
    }

    /// <summary>
    /// Gets all page sizes from a Base64-encoded PDF.
    /// </summary>
    /// <param name="pdfBase64">Base64-encoded PDF file data.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>Page sizes in PDF points.</returns>
    public static IReadOnlyList<PdfPageSize> GetPageSizesFromBase64(string pdfBase64, string? password = null)
    {
        if (pdfBase64 is null)
        {
            throw new ArgumentNullException(nameof(pdfBase64));
        }

        return GetPageSizes(Convert.FromBase64String(pdfBase64), password);
    }

    /// <summary>
    /// Renders a zero-based page from a PDF file using page render options.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional page render options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The rendered page bitmap.</returns>
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

    /// <summary>
    /// Renders a zero-based page from a PDF file using conversion options.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The rendered page bitmap.</returns>
    public static PdfBitmap RenderPage(
        string pdfPath,
        int pageIndex,
        PdfImageConversionOptions? options,
        string? password = null)
    {
        options ??= new PdfImageConversionOptions();

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);
        return RenderPage(document, pageIndex, options);
    }

    /// <summary>
    /// Renders a zero-based page from PDF bytes.
    /// </summary>
    /// <param name="pdfBytes">PDF file bytes.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The rendered page bitmap.</returns>
    public static PdfBitmap RenderPage(
        byte[] pdfBytes,
        int pageIndex,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        options ??= new PdfImageConversionOptions();

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfBytes, password);
        return RenderPage(document, pageIndex, options);
    }

    /// <summary>
    /// Renders a zero-based page from a PDF stream.
    /// </summary>
    /// <param name="pdfStream">Stream containing PDF file data.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="pdfStream"/> open after loading.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The rendered page bitmap.</returns>
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
        return RenderPage(document, pageIndex, options);
    }

    /// <summary>
    /// Renders a zero-based page from an already open document.
    /// </summary>
    /// <param name="document">Open document owned by the caller.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional page render options.</param>
    /// <returns>The rendered page bitmap.</returns>
    /// <remarks>
    /// Use this overload for repeated rendering to avoid reopening the document. The caller must keep both the
    /// document and its <see cref="PdfiumLibrary" /> initialization reference alive for the duration of the call.
    /// The returned bitmap owns its full managed pixel buffer.
    /// </remarks>
    public static PdfBitmap RenderPage(
        PdfDocument document,
        int pageIndex,
        PdfPageRenderOptions? options = null)
    {
        return RenderPage(document, pageIndex, new PdfImageConversionOptions
        {
            Render = options ?? new PdfPageRenderOptions(),
        });
    }

    /// <summary>
    /// Renders a zero-based page from an already open document using conversion options.
    /// </summary>
    /// <param name="document">Open document owned by the caller.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <returns>The rendered page bitmap.</returns>
    /// <remarks>
    /// Use this overload for repeated rendering to avoid reopening the document. The caller must keep both the
    /// document and its <see cref="PdfiumLibrary" /> initialization reference alive for the duration of the call.
    /// The returned bitmap owns its full managed pixel buffer.
    /// </remarks>
    public static PdfBitmap RenderPage(
        PdfDocument document,
        int pageIndex,
        PdfImageConversionOptions? options)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        options ??= new PdfImageConversionOptions();
        using var page = document.LoadPage(pageIndex);

        var bitmap = page.Render(GetRenderOptions(options));
        ApplyConversionColorMode(bitmap, options);

        return bitmap;
    }

    /// <summary>
    /// Renders a zero-based page from a PDF file into an existing bitmap.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="destination">Destination bitmap whose dimensions must match the configured render size in pixels.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void RenderPageInto(
        string pdfPath,
        int pageIndex,
        PdfBitmap destination,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        options ??= new PdfImageConversionOptions();

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);
        RenderPageInto(document, pageIndex, destination, options);
    }

    /// <summary>
    /// Renders a zero-based page from an already open document into an existing bitmap.
    /// </summary>
    /// <param name="document">Open document owned by the caller.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="destination">Destination bitmap whose dimensions must match the configured render size in pixels.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <remarks>
    /// Use this overload for repeated rendering to avoid reopening the document and reallocating the destination
    /// pixels. The caller must keep the document and its <see cref="PdfiumLibrary" /> initialization reference alive.
    /// </remarks>
    public static void RenderPageInto(
        PdfDocument document,
        int pageIndex,
        PdfBitmap destination,
        PdfImageConversionOptions? options = null)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        options ??= new PdfImageConversionOptions();
        using var page = document.LoadPage(pageIndex);
        RenderPageInto(page, destination, options);
    }

    /// <summary>
    /// Renders a zero-based page from a Base64-encoded PDF.
    /// </summary>
    /// <param name="pdfBase64">Base64-encoded PDF file data.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The rendered page bitmap.</returns>
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

    /// <summary>
    /// Renders a one-based page number from a PDF file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The rendered page bitmap.</returns>
    public static PdfBitmap RenderPageNumber(
        string pdfPath,
        int pageNumber,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        return RenderPage(pdfPath, ToPageIndex(pageNumber), options, password);
    }

    /// <summary>
    /// Renders a one-based page number from an already open document.
    /// </summary>
    /// <param name="document">Open document owned by the caller.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <returns>The rendered page bitmap.</returns>
    public static PdfBitmap RenderPageNumber(
        PdfDocument document,
        int pageNumber,
        PdfImageConversionOptions? options = null)
    {
        return RenderPage(document, ToPageIndex(pageNumber), options);
    }

    /// <summary>
    /// Renders a one-based page number from a PDF file into an existing bitmap.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="destination">Destination bitmap whose dimensions must match the configured render size in pixels.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void RenderPageNumberInto(
        string pdfPath,
        int pageNumber,
        PdfBitmap destination,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        RenderPageInto(pdfPath, ToPageIndex(pageNumber), destination, options, password);
    }

    /// <summary>
    /// Renders a one-based page number from an already open document into an existing bitmap.
    /// </summary>
    /// <param name="document">Open document owned by the caller.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="destination">Destination bitmap whose dimensions must match the configured render size in pixels.</param>
    /// <param name="options">Optional conversion options.</param>
    public static void RenderPageNumberInto(
        PdfDocument document,
        int pageNumber,
        PdfBitmap destination,
        PdfImageConversionOptions? options = null)
    {
        RenderPageInto(document, ToPageIndex(pageNumber), destination, options);
    }

    /// <summary>
    /// Renders a one-based page number from PDF bytes.
    /// </summary>
    /// <param name="pdfBytes">PDF file bytes.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The rendered page bitmap.</returns>
    public static PdfBitmap RenderPageNumber(
        byte[] pdfBytes,
        int pageNumber,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        return RenderPage(pdfBytes, ToPageIndex(pageNumber), options, password);
    }

    /// <summary>
    /// Renders and saves a zero-based page from a PDF file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imagePath">Destination image path.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
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
        SavePageDirect(pdfPath, pageIndex, imagePath, options, password);
    }

    /// <summary>
    /// Renders and writes a zero-based page from a PDF file to a stream.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imageStream">Destination image stream.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void SavePage(
        string pdfPath,
        int pageIndex,
        Stream imageStream,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        if (imageStream is null)
        {
            throw new ArgumentNullException(nameof(imageStream));
        }

        options ??= new PdfImageConversionOptions();
        SavePageDirect(pdfPath, pageIndex, imageStream, options, password);
    }

    /// <summary>
    /// Renders and saves a one-based page number from a PDF file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="imagePath">Destination image path.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void SavePageNumber(
        string pdfPath,
        int pageNumber,
        string imagePath,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        SavePage(pdfPath, ToPageIndex(pageNumber), imagePath, options, password);
    }

    /// <summary>
    /// Renders and writes a one-based page number from a PDF file to a stream.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="imageStream">Destination image stream.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void SavePageNumber(
        string pdfPath,
        int pageNumber,
        Stream imageStream,
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        SavePage(pdfPath, ToPageIndex(pageNumber), imageStream, options, password);
    }

    /// <summary>
    /// Renders and saves a one-based page number as PNG.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="imagePath">Destination image path.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void SavePng(string pdfPath, int pageNumber, string imagePath, PdfImageConversionOptions? options = null, string? password = null)
    {
        SavePageNumber(pdfPath, pageNumber, imagePath, WithFormat(options, PdfImageOutputFormat.Png), password);
    }

    /// <summary>
    /// Renders and writes a one-based page number as PNG to a stream.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="imageStream">Destination image stream.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void SavePng(string pdfPath, int pageNumber, Stream imageStream, PdfImageConversionOptions? options = null, string? password = null)
    {
        SavePageNumber(pdfPath, pageNumber, imageStream, WithFormat(options, PdfImageOutputFormat.Png), password);
    }

    /// <summary>
    /// Renders and saves a one-based page number as JPEG.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="imagePath">Destination image path.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void SaveJpeg(string pdfPath, int pageNumber, string imagePath, PdfImageConversionOptions? options = null, string? password = null)
    {
        SavePageNumber(pdfPath, pageNumber, imagePath, WithFormat(options, PdfImageOutputFormat.Jpeg), password);
    }

    /// <summary>
    /// Renders and writes a one-based page number as JPEG to a stream.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="imageStream">Destination image stream.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void SaveJpeg(string pdfPath, int pageNumber, Stream imageStream, PdfImageConversionOptions? options = null, string? password = null)
    {
        SavePageNumber(pdfPath, pageNumber, imageStream, WithFormat(options, PdfImageOutputFormat.Jpeg), password);
    }

    /// <summary>
    /// Renders and saves a one-based page number as WebP.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="imagePath">Destination image path.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void SaveWebp(string pdfPath, int pageNumber, string imagePath, PdfImageConversionOptions? options = null, string? password = null)
    {
        SavePageNumber(pdfPath, pageNumber, imagePath, WithFormat(options, PdfImageOutputFormat.Webp), password);
    }

    /// <summary>
    /// Renders and writes a one-based page number as WebP to a stream.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumber">One-based page number.</param>
    /// <param name="imageStream">Destination image stream.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    public static void SaveWebp(string pdfPath, int pageNumber, Stream imageStream, PdfImageConversionOptions? options = null, string? password = null)
    {
        SavePageNumber(pdfPath, pageNumber, imageStream, WithFormat(options, PdfImageOutputFormat.Webp), password);
    }

    /// <summary>
    /// Lazily renders every page in a PDF file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>A sequence of rendered page bitmaps.</returns>
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

    /// <summary>
    /// Lazily renders selected zero-based page indexes from a PDF file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageIndexes">Zero-based page indexes to render.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>A sequence of rendered page bitmaps ordered by page index.</returns>
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

    /// <summary>
    /// Renders every page in a PDF file and saves each page to an image file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="outputDirectory">Directory where rendered images are written.</param>
    /// <param name="fileNamePrefix">Prefix used for generated image file names.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The number of pages saved.</returns>
    /// <remarks>
    /// Multi-page PNG, JPEG, and WebP exports overlap serialized PDFium rendering with encoding and retain at most two
    /// full rendered page buffers. BMP and single-page exports use one buffer and run sequentially. If an operation
    /// fails, files completed before the failure remain in the output directory.
    /// </remarks>
    public static int SaveDocument(
        string pdfPath,
        string outputDirectory,
        string fileNamePrefix = "page",
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);

        return SaveAllPages(document, outputDirectory, fileNamePrefix, options);
    }

    /// <summary>
    /// Renders selected zero-based pages from a PDF file and saves each page to an image file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageIndexes">Zero-based page indexes to save.</param>
    /// <param name="outputDirectory">Directory where rendered images are written.</param>
    /// <param name="fileNamePrefix">Prefix used for generated image file names.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The number of pages saved.</returns>
    /// <remarks>
    /// Multi-page PNG, JPEG, and WebP exports overlap serialized PDFium rendering with encoding and retain at most two
    /// full rendered page buffers. BMP and single-page exports use one buffer and run sequentially. Generated file
    /// names remain deterministic even though compressed files may finish encoding out of order.
    /// </remarks>
    public static int SavePages(
        string pdfPath,
        IEnumerable<int> pageIndexes,
        string outputDirectory,
        string fileNamePrefix = "page",
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        if (pageIndexes is null)
        {
            throw new ArgumentNullException(nameof(pageIndexes));
        }

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);

        return SavePages(document, pageIndexes, outputDirectory, fileNamePrefix, options);
    }

    /// <summary>
    /// Renders selected one-based page numbers from a PDF file and saves each page to an image file.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="pageNumbers">One-based page numbers to save.</param>
    /// <param name="outputDirectory">Directory where rendered images are written.</param>
    /// <param name="fileNamePrefix">Prefix used for generated image file names.</param>
    /// <param name="options">Optional conversion options.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>The number of pages saved.</returns>
    /// <remarks>
    /// Multi-page PNG, JPEG, and WebP exports overlap serialized PDFium rendering with encoding and retain at most two
    /// full rendered page buffers. BMP and single-page exports use one buffer and run sequentially. Generated file
    /// names remain deterministic even though compressed files may finish encoding out of order.
    /// </remarks>
    public static int SavePageNumbers(
        string pdfPath,
        IEnumerable<int> pageNumbers,
        string outputDirectory,
        string fileNamePrefix = "page",
        PdfImageConversionOptions? options = null,
        string? password = null)
    {
        if (pageNumbers is null)
        {
            throw new ArgumentNullException(nameof(pageNumbers));
        }

        return SavePages(
            pdfPath,
            pageNumbers.Select(ToPageIndex),
            outputDirectory,
            fileNamePrefix,
            options,
            password);
    }

    /// <summary>
    /// Saves a rendered bitmap to a file in the requested image format.
    /// </summary>
    /// <param name="bitmap">Rendered bitmap to save.</param>
    /// <param name="path">Destination image path.</param>
    /// <param name="format">Image output format.</param>
    public static void SaveBitmap(PdfBitmap bitmap, string path, PdfImageOutputFormat format)
    {
        SaveBitmap(bitmap, path, format, new PdfImageEncodingOptions());
    }

    /// <summary>
    /// Saves a rendered bitmap to a file in the requested image format.
    /// </summary>
    /// <param name="bitmap">Rendered bitmap to save.</param>
    /// <param name="path">Destination image path.</param>
    /// <param name="format">Image output format.</param>
    /// <param name="encoding">Compressed image encoding settings. BMP output ignores these settings.</param>
    public static void SaveBitmap(
        PdfBitmap bitmap,
        string path,
        PdfImageOutputFormat format,
        PdfImageEncodingOptions encoding)
    {
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        if (encoding is null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        switch (format)
        {
            case PdfImageOutputFormat.Bmp:
                PdfImageWriter.SaveBmp(bitmap, path);
                break;
            case PdfImageOutputFormat.Png:
                PdfImageWriter.SavePng(bitmap, path, encoding);
                break;
            case PdfImageOutputFormat.Jpeg:
                PdfImageWriter.SaveJpeg(bitmap, path, encoding);
                break;
            case PdfImageOutputFormat.Webp:
                PdfImageWriter.SaveWebp(bitmap, path, encoding);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Image format is not supported.");
        }
    }

    /// <summary>
    /// Writes a rendered bitmap to a stream in the requested image format.
    /// </summary>
    /// <param name="bitmap">Rendered bitmap to save.</param>
    /// <param name="stream">Destination image stream.</param>
    /// <param name="format">Image output format.</param>
    public static void SaveBitmap(PdfBitmap bitmap, Stream stream, PdfImageOutputFormat format)
    {
        SaveBitmap(bitmap, stream, format, new PdfImageEncodingOptions());
    }

    /// <summary>
    /// Writes a rendered bitmap to a stream in the requested image format without closing the stream.
    /// </summary>
    /// <param name="bitmap">Rendered bitmap to save.</param>
    /// <param name="stream">Destination image stream, which remains open.</param>
    /// <param name="format">Image output format.</param>
    /// <param name="encoding">Compressed image encoding settings. BMP output ignores these settings.</param>
    public static void SaveBitmap(
        PdfBitmap bitmap,
        Stream stream,
        PdfImageOutputFormat format,
        PdfImageEncodingOptions encoding)
    {
        if (bitmap is null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (encoding is null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }

        switch (format)
        {
            case PdfImageOutputFormat.Bmp:
                PdfImageWriter.WriteBmp(bitmap, stream);
                break;
            case PdfImageOutputFormat.Png:
                PdfImageWriter.WritePng(bitmap, stream, encoding);
                break;
            case PdfImageOutputFormat.Jpeg:
                PdfImageWriter.WriteJpeg(bitmap, stream, encoding);
                break;
            case PdfImageOutputFormat.Webp:
                PdfImageWriter.WriteWebp(bitmap, stream, encoding);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Image format is not supported.");
        }
    }

    /// <summary>
    /// Applies color post-processing to a rendered bitmap in place.
    /// </summary>
    /// <param name="bitmap">Bitmap to modify.</param>
    /// <param name="colorMode">Color conversion to apply.</param>
    /// <param name="blackAndWhiteThreshold">Luminance threshold used when <paramref name="colorMode"/> is black and white.</param>
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

    internal static PdfPageRenderOptions GetRenderOptions(PdfImageConversionOptions options)
    {
        var source = options.Render ?? new PdfPageRenderOptions();
        if (options.ColorMode == PdfImageColorMode.Color)
        {
            return source;
        }

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

    internal static void ApplyConversionColorMode(PdfBitmap bitmap, PdfImageConversionOptions options)
    {
        switch (options.ColorMode)
        {
            case PdfImageColorMode.Color:
            case PdfImageColorMode.Grayscale:
                return;
            case PdfImageColorMode.BlackAndWhite:
                ApplyBlackAndWhiteFromGrayscale(bitmap, options.BlackAndWhiteThreshold);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(options.ColorMode), options.ColorMode,
                    "Image color mode is not supported.");
        }
    }

    private static void ApplyGrayscale(PdfBitmap bitmap)
    {
        var pixels = bitmap.Pixels;

        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowOffset = y * bitmap.Stride;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var offset = rowOffset + x * 4;
                var gray = GetLuminance(pixels[offset + 2], pixels[offset + 1], pixels[offset]);

                pixels[offset] = gray;
                pixels[offset + 1] = gray;
                pixels[offset + 2] = gray;
            }
        }
    }

    private static void ApplyBlackAndWhite(PdfBitmap bitmap, byte threshold)
    {
        var pixels = bitmap.Pixels;

        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowOffset = y * bitmap.Stride;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var offset = rowOffset + x * 4;
                var gray = GetLuminance(pixels[offset + 2], pixels[offset + 1], pixels[offset]);
                var value = gray >= threshold ? byte.MaxValue : byte.MinValue;

                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
            }
        }
    }

    private static void ApplyBlackAndWhiteFromGrayscale(PdfBitmap bitmap, byte threshold)
    {
        var pixels = bitmap.Pixels;

        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowOffset = y * bitmap.Stride;

            for (var x = 0; x < bitmap.Width; x++)
            {
                var offset = rowOffset + x * 4;
                var value = pixels[offset] >= threshold ? byte.MaxValue : byte.MinValue;

                pixels[offset] = value;
                pixels[offset + 1] = value;
                pixels[offset + 2] = value;
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

    internal static void RenderPageInto(PdfPage page, PdfBitmap destination, PdfImageConversionOptions options)
    {
        var renderOptions = GetRenderOptions(options);
        page.Render(destination, renderOptions);
        ApplyConversionColorMode(destination, options);
    }

    private static void SavePageDirect(
        string pdfPath,
        int pageIndex,
        string imagePath,
        PdfImageConversionOptions options,
        string? password)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);
        using var page = document.LoadPage(pageIndex);

        var renderOptions = GetRenderOptions(options);
        using var bitmapLease = RentBitmapLease(page, renderOptions);
        var bitmap = RenderToLease(page, bitmapLease, renderOptions, options);

        SaveBitmap(bitmap, imagePath, options.Format, GetEncodingOptions(options));
    }

    private static void SavePageDirect(
        string pdfPath,
        int pageIndex,
        Stream imageStream,
        PdfImageConversionOptions options,
        string? password)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath, password);
        using var page = document.LoadPage(pageIndex);

        var renderOptions = GetRenderOptions(options);
        using var bitmapLease = RentBitmapLease(page, renderOptions);
        var bitmap = RenderToLease(page, bitmapLease, renderOptions, options);

        SaveBitmap(bitmap, imageStream, options.Format, GetEncodingOptions(options));
    }

    private static IEnumerable<PdfBitmap> RenderPages(
        PdfDocument document,
        IEnumerable<int>? pageIndexes,
        PdfImageConversionOptions? options)
    {
        options ??= new PdfImageConversionOptions();
        var renderOptions = GetRenderOptions(options);

        if (pageIndexes is null)
        {
            for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
            {
                using var page = document.LoadPage(pageIndex);
                var bitmap = page.Render(renderOptions);
                ApplyConversionColorMode(bitmap, options);

                yield return bitmap;
            }
        }
        else
        {
            foreach (var pageIndex in pageIndexes.OrderBy(page => page).Distinct())
            {
                using var page = document.LoadPage(pageIndex);
                var bitmap = page.Render(renderOptions);
                ApplyConversionColorMode(bitmap, options);

                yield return bitmap;
            }
        }
    }

    private static int SaveAllPages(
        PdfDocument document,
        string outputDirectory,
        string fileNamePrefix,
        PdfImageConversionOptions? options)
    {
        ValidateOutput(outputDirectory, fileNamePrefix);
        Directory.CreateDirectory(outputDirectory);

        options ??= new PdfImageConversionOptions();
        var pageCount = document.PageCount;
        return SavePagesCore(
            document,
            Enumerable.Range(0, pageCount),
            pageCount,
            outputDirectory,
            fileNamePrefix,
            options,
            usePipelinedEncoding: ShouldUsePipelinedEncoding(options.Format, pageCount),
            SaveBitmap);
    }

    private static int SavePages(
        PdfDocument document,
        IEnumerable<int> pageIndexes,
        string outputDirectory,
        string fileNamePrefix,
        PdfImageConversionOptions? options)
    {
        ValidateOutput(outputDirectory, fileNamePrefix);
        Directory.CreateDirectory(outputDirectory);

        options ??= new PdfImageConversionOptions();
        var selectedPageIndexes = pageIndexes.OrderBy(page => page).Distinct().ToArray();
        return SavePagesCore(
            document,
            selectedPageIndexes,
            selectedPageIndexes.Length,
            outputDirectory,
            fileNamePrefix,
            options,
            usePipelinedEncoding: ShouldUsePipelinedEncoding(options.Format, selectedPageIndexes.Length),
            SaveBitmap);
    }

    internal static int SavePagesCore(
        PdfDocument document,
        IEnumerable<int> pageIndexes,
        int pageCount,
        string outputDirectory,
        string fileNamePrefix,
        PdfImageConversionOptions options,
        bool usePipelinedEncoding,
        PdfBatchBitmapEncoder encoder)
    {
        var renderOptions = GetRenderOptions(options);
        var encodingOptions = GetEncodingOptions(options);
        var extension = GetExtension(options.Format);

        if (usePipelinedEncoding)
        {
            return SavePagesPipelined(
                document,
                pageIndexes,
                pageCount,
                outputDirectory,
                fileNamePrefix,
                options,
                renderOptions,
                encodingOptions,
                extension,
                encoder);
        }

        return SavePagesSequential(
            document,
            pageIndexes,
            pageCount,
            outputDirectory,
            fileNamePrefix,
            options,
            renderOptions,
            encodingOptions,
            extension,
            encoder);
    }

    private static int SavePagesSequential(
        PdfDocument document,
        IEnumerable<int> pageIndexes,
        int pageCount,
        string outputDirectory,
        string fileNamePrefix,
        PdfImageConversionOptions options,
        PdfPageRenderOptions renderOptions,
        PdfImageEncodingOptions encodingOptions,
        string extension,
        PdfBatchBitmapEncoder encoder)
    {
        PdfBitmapLease? bitmapLease = null;

        try
        {
            foreach (var pageIndex in pageIndexes)
            {
                using var page = document.LoadPage(pageIndex);
                bitmapLease = EnsureBitmapLease(bitmapLease, page, renderOptions);
                var bitmap = RenderToLease(page, bitmapLease, renderOptions, options);
                var imagePath = Path.Combine(outputDirectory, $"{fileNamePrefix}-{pageIndex + 1:D4}{extension}");

                encoder(bitmap, imagePath, options.Format, encodingOptions);
            }
        }
        finally
        {
            bitmapLease?.Dispose();
        }

        return pageCount;
    }

    private static int SavePagesPipelined(
        PdfDocument document,
        IEnumerable<int> pageIndexes,
        int pageCount,
        string outputDirectory,
        string fileNamePrefix,
        PdfImageConversionOptions options,
        PdfPageRenderOptions renderOptions,
        PdfImageEncodingOptions encodingOptions,
        string extension,
        PdfBatchBitmapEncoder encoder)
    {
        using var pipeline = new PdfBatchSavePipeline(encoder);

        foreach (var pageIndex in pageIndexes)
        {
            var slot = pipeline.AcquireSlot();
            try
            {
                using var page = document.LoadPage(pageIndex);
                var bitmapLease = slot.EnsureLease(page, renderOptions);
                RenderToLease(page, bitmapLease, renderOptions, options);
                var imagePath = Path.Combine(outputDirectory, $"{fileNamePrefix}-{pageIndex + 1:D4}{extension}");

                pipeline.Queue(slot, imagePath, options.Format, encodingOptions);
            }
            catch
            {
                pipeline.ReturnSlot(slot);
                throw;
            }
        }

        pipeline.Complete();
        return pageCount;
    }

    private static bool ShouldUsePipelinedEncoding(PdfImageOutputFormat format, int pageCount)
    {
        return pageCount >= 2 && format is PdfImageOutputFormat.Png or PdfImageOutputFormat.Jpeg or
            PdfImageOutputFormat.Webp;
    }

    internal static PdfBitmapLease EnsureBitmapLease(
        PdfBitmapLease? bitmapLease,
        PdfPage page,
        PdfPageRenderOptions renderOptions)
    {
        var (width, height) = renderOptions.GetPixelSize(page.Width, page.Height);
        if (bitmapLease is not null)
        {
            var bitmap = bitmapLease.Bitmap;
            if (bitmap.Width == width && bitmap.Height == height)
            {
                return bitmapLease;
            }

            bitmapLease.Dispose();
        }

        return PdfBitmapLease.Rent(width, height, clear: false);
    }

    private static PdfBitmapLease RentBitmapLease(PdfPage page, PdfPageRenderOptions renderOptions)
    {
        var (width, height) = renderOptions.GetPixelSize(page.Width, page.Height);
        return PdfBitmapLease.Rent(width, height, clear: false);
    }

    internal static PdfBitmap RenderToLease(
        PdfPage page,
        PdfBitmapLease bitmapLease,
        PdfPageRenderOptions renderOptions,
        PdfImageConversionOptions options)
    {
        var bitmap = bitmapLease.Bitmap;
        if (!renderOptions.FillBackground)
        {
            ClearPixelRegion(bitmap);
        }

        page.Render(bitmapLease, renderOptions);
        ApplyConversionColorMode(bitmap, options);

        return bitmap;
    }

    private static void ClearPixelRegion(PdfBitmap bitmap)
    {
        Array.Clear(bitmap.Pixels, 0, checked(bitmap.Stride * bitmap.Height));
    }

    internal static PdfImageEncodingOptions GetEncodingOptions(PdfImageConversionOptions options)
    {
        return options.Encoding ?? new PdfImageEncodingOptions();
    }

    private static void ValidateOutput(string outputDirectory, string fileNamePrefix)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("Output directory cannot be null or whitespace.", nameof(outputDirectory));
        }

        if (string.IsNullOrWhiteSpace(fileNamePrefix))
        {
            throw new ArgumentException("File name prefix cannot be null or whitespace.", nameof(fileNamePrefix));
        }
    }

    private static PdfImageConversionOptions WithFormat(PdfImageConversionOptions? options, PdfImageOutputFormat format)
    {
        options ??= new PdfImageConversionOptions();
        options.Format = format;
        return options;
    }
}
