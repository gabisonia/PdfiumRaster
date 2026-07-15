namespace PdfiumRaster;

/// <summary>
/// Reuses an open PDF document, loaded page, and render buffer across synchronous rendering operations.
/// </summary>
/// <remarks>
/// Page indexes are zero-based. A session permits one operation at a time; concurrent and reentrant operations
/// throw <see cref="InvalidOperationException" />. Dispose the session to release its native PDFium resources.
/// </remarks>
public sealed class PdfRenderSession : IDisposable
{
    private readonly PdfiumLibrary _library;
    private readonly PdfDocument _document;
    private PdfPage? _cachedPage;
    private int _cachedPageIndex = -1;
    private PdfBitmapLease? _bitmapLease;
    private int _operationActive;
    private int _disposed;

    private PdfRenderSession(PdfiumLibrary library, PdfDocument document)
    {
        _library = library;
        _document = document;
    }

    /// <summary>
    /// Gets the number of pages in the open PDF document.
    /// </summary>
    public int PageCount
    {
        get
        {
            EnterOperation();
            try
            {
                return _document.PageCount;
            }
            finally
            {
                ExitOperation();
            }
        }
    }

    /// <summary>
    /// Opens a render session from a PDF file path.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>A render session that owns the open PDF document.</returns>
    public static PdfRenderSession Open(string pdfPath, string? password = null)
    {
        var library = PdfiumLibrary.Initialize();
        try
        {
            return new PdfRenderSession(library, PdfDocument.Load(pdfPath, password));
        }
        catch
        {
            library.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a render session from PDF bytes.
    /// </summary>
    /// <param name="pdfBytes">PDF file bytes, which remain pinned until the session is disposed.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>A render session that owns the open PDF document.</returns>
    /// <remarks>The full PDF byte array remains in managed memory for the session lifetime.</remarks>
    public static PdfRenderSession Open(byte[] pdfBytes, string? password = null)
    {
        var library = PdfiumLibrary.Initialize();
        try
        {
            return new PdfRenderSession(library, PdfDocument.Load(pdfBytes, password));
        }
        catch
        {
            library.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a render session from a PDF stream.
    /// </summary>
    /// <param name="pdfStream">Readable PDF stream. Seekable streams are accessed without copying the full PDF.</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="pdfStream" /> open when the session is disposed.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>A render session that owns the open PDF document.</returns>
    /// <remarks>Non-seekable streams are buffered in managed memory because PDFium requires random access.</remarks>
    public static PdfRenderSession Open(Stream pdfStream, bool leaveOpen = false, string? password = null)
    {
        var library = PdfiumLibrary.Initialize();
        try
        {
            return new PdfRenderSession(library, PdfDocument.Load(pdfStream, leaveOpen, password));
        }
        catch
        {
            library.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Renders a zero-based page into a newly allocated, caller-owned bitmap.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional rendering and conversion settings.</param>
    /// <returns>A bitmap with an independently owned managed pixel buffer.</returns>
    public PdfBitmap RenderPage(int pageIndex, PdfImageConversionOptions? options = null)
    {
        options ??= new PdfImageConversionOptions();
        EnterOperation();
        try
        {
            var page = GetPage(pageIndex);
            var bitmap = page.Render(PdfImageConverter.GetRenderOptions(options));
            PdfImageConverter.ApplyConversionColorMode(bitmap, options);
            return bitmap;
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Renders a zero-based page into a caller-owned destination bitmap.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="destination">Destination whose pixel dimensions must match the configured render size.</param>
    /// <param name="options">Optional rendering and conversion settings.</param>
    public void RenderPageInto(
        int pageIndex,
        PdfBitmap destination,
        PdfImageConversionOptions? options = null)
    {
        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        options ??= new PdfImageConversionOptions();
        EnterOperation();
        try
        {
            PdfImageConverter.RenderPageInto(GetPage(pageIndex), destination, options);
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Renders a zero-based page into the session buffer and invokes a synchronous callback.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="callback">Callback that consumes the rendered bitmap before this method returns.</param>
    /// <param name="options">Optional rendering and conversion settings.</param>
    /// <remarks>
    /// The bitmap and its pixels are owned by the session and are valid only for the callback duration. Do not retain
    /// them. The callback is synchronous; callback exceptions propagate and the session remains usable.
    /// </remarks>
    public void RenderPage(
        int pageIndex,
        Action<PdfBitmap> callback,
        PdfImageConversionOptions? options = null)
    {
        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        options ??= new PdfImageConversionOptions();
        EnterOperation();
        try
        {
            callback(RenderToSessionBuffer(pageIndex, options));
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Renders a zero-based page into the session buffer and invokes a synchronous result callback.
    /// </summary>
    /// <typeparam name="TResult">Callback result type.</typeparam>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="callback">Callback that consumes the rendered bitmap before this method returns.</param>
    /// <param name="options">Optional rendering and conversion settings.</param>
    /// <returns>The callback result.</returns>
    /// <remarks>
    /// The bitmap and its pixels are owned by the session and are valid only for the callback duration. Do not retain
    /// them. The callback is synchronous; callback exceptions propagate and the session remains usable.
    /// </remarks>
    public TResult RenderPage<TResult>(
        int pageIndex,
        Func<PdfBitmap, TResult> callback,
        PdfImageConversionOptions? options = null)
    {
        if (callback is null)
        {
            throw new ArgumentNullException(nameof(callback));
        }

        options ??= new PdfImageConversionOptions();
        EnterOperation();
        try
        {
            return callback(RenderToSessionBuffer(pageIndex, options));
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Renders and saves a zero-based page to an image file.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imagePath">Destination image path.</param>
    /// <param name="options">Optional rendering, conversion, format, and encoding settings.</param>
    public void SavePage(int pageIndex, string imagePath, PdfImageConversionOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path cannot be null or whitespace.", nameof(imagePath));
        }

        options ??= new PdfImageConversionOptions();
        EnterOperation();
        try
        {
            var bitmap = RenderToSessionBuffer(pageIndex, options);
            PdfImageConverter.SaveBitmap(
                bitmap,
                imagePath,
                options.Format,
                PdfImageConverter.GetEncodingOptions(options));
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Renders and writes a zero-based page to an image stream without closing the stream.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imageStream">Destination stream, which remains open.</param>
    /// <param name="options">Optional rendering, conversion, format, and encoding settings.</param>
    public void SavePage(int pageIndex, Stream imageStream, PdfImageConversionOptions? options = null)
    {
        if (imageStream is null)
        {
            throw new ArgumentNullException(nameof(imageStream));
        }

        options ??= new PdfImageConversionOptions();
        EnterOperation();
        try
        {
            var bitmap = RenderToSessionBuffer(pageIndex, options);
            PdfImageConverter.SaveBitmap(
                bitmap,
                imageStream,
                options.Format,
                PdfImageConverter.GetEncodingOptions(options));
        }
        finally
        {
            ExitOperation();
        }
    }

    /// <summary>
    /// Releases the cached bitmap, page, document, and PDFium initialization reference.
    /// </summary>
    public void Dispose()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _operationActive, 1, 0) != 0)
        {
            throw new InvalidOperationException("The render session already has an active operation.");
        }

        try
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _bitmapLease?.Dispose();
            _bitmapLease = null;
            _cachedPage?.Dispose();
            _cachedPage = null;
            _document.Dispose();
            _library.Dispose();
        }
        finally
        {
            ExitOperation();
        }
    }

    private PdfBitmap RenderToSessionBuffer(int pageIndex, PdfImageConversionOptions options)
    {
        var page = GetPage(pageIndex);
        var renderOptions = PdfImageConverter.GetRenderOptions(options);
        _bitmapLease = PdfImageConverter.EnsureBitmapLease(_bitmapLease, page, renderOptions);
        return PdfImageConverter.RenderToLease(page, _bitmapLease, renderOptions, options);
    }

    private PdfPage GetPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _document.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex,
                "Page index is outside the document page range.");
        }

        if (_cachedPage is not null && _cachedPageIndex == pageIndex)
        {
            return _cachedPage;
        }

        var page = _document.LoadPage(pageIndex);
        _cachedPage?.Dispose();
        _cachedPage = page;
        _cachedPageIndex = pageIndex;
        return page;
    }

    private void EnterOperation()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (Interlocked.CompareExchange(ref _operationActive, 1, 0) != 0)
        {
            throw new InvalidOperationException("The render session already has an active operation.");
        }

        if (Volatile.Read(ref _disposed) != 0)
        {
            ExitOperation();
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private void ExitOperation()
    {
        Volatile.Write(ref _operationActive, 0);
    }
}
