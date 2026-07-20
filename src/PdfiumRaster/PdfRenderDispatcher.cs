using System.Threading.Channels;

namespace PdfiumRaster;

/// <summary>
/// Accepts concurrent PDF page requests through a bounded asynchronous queue while keeping PDFium calls serialized.
/// </summary>
/// <remarks>
/// Page indexes are zero-based. One dispatcher is normally shared by all requests in a process. PDF loading and
/// rendering execute one at a time because PDFium is not thread-safe; completed page bitmaps may be encoded in
/// parallel. Use <see cref="PdfRenderSession" /> instead when repeatedly rendering pages from one open document.
/// </remarks>
public sealed class PdfRenderDispatcher : IDisposable
{
    private readonly PdfiumLibrary _library;
    private readonly Channel<RenderJob> _renderQueue;
    private readonly Channel<EncodingWork> _encodingQueue;
    private readonly SemaphoreSlim _encodingSlots;
    private readonly PdfRenderQueueFullMode _queueFullMode;
    private readonly Task _nativeWorker;
    private readonly Task[] _encodingWorkers;
    private readonly Task _completion;
    private int _accepting = 1;
    private int _cancelRequested;
    private int _disposed;

    /// <summary>
    /// Initializes a concurrent render dispatcher.
    /// </summary>
    /// <param name="options">Optional queue capacity, backpressure, and encoding concurrency settings.</param>
    public PdfRenderDispatcher(PdfRenderDispatcherOptions? options = null)
    {
        options ??= new PdfRenderDispatcherOptions();
        var queueCapacity = options.QueueCapacity;
        var encodingConcurrency = options.EncodingConcurrency;
        _queueFullMode = options.QueueFullMode;

        _renderQueue = Channel.CreateBounded<RenderJob>(new BoundedChannelOptions(queueCapacity)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        _encodingQueue = Channel.CreateUnbounded<EncodingWork>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = encodingConcurrency == 1,
            SingleWriter = true,
        });
        _encodingSlots = new SemaphoreSlim(encodingConcurrency, encodingConcurrency);
        _library = PdfiumLibrary.Initialize();

        _encodingWorkers = new Task[encodingConcurrency];
        for (var index = 0; index < _encodingWorkers.Length; index++)
        {
            _encodingWorkers[index] = Task.Run(ProcessEncodingQueueAsync);
        }

        _nativeWorker = Task.Run(ProcessRenderQueueAsync);
        _completion = CompletePipelineAsync();
    }

    /// <summary>
    /// Queues a zero-based page from a PDF file and returns an independently owned bitmap.
    /// </summary>
    /// <param name="pdfPath">Path opened when this request reaches the PDFium stage.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional rendering and color-conversion settings, snapshotted during submission.</param>
    /// <param name="password">Optional document password.</param>
    /// <param name="cancellationToken">Cancels queue waiting or work that has not entered an uninterruptible stage.</param>
    /// <returns>A task that produces a caller-owned BGRA bitmap.</returns>
    public Task<PdfBitmap> RenderPageAsync(
        string pdfPath,
        int pageIndex,
        PdfImageConversionOptions? options = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        return SubmitRender(CreatePathSource(pdfPath), pageIndex, options, password, cancellationToken);
    }

    /// <summary>
    /// Queues a zero-based page from PDF bytes and returns an independently owned bitmap.
    /// </summary>
    /// <param name="pdfBytes">PDF bytes that must not be modified until the returned task completes.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional rendering and color-conversion settings, snapshotted during submission.</param>
    /// <param name="password">Optional document password.</param>
    /// <param name="cancellationToken">Cancels queue waiting or work that has not entered an uninterruptible stage.</param>
    /// <returns>A task that produces a caller-owned BGRA bitmap.</returns>
    /// <remarks>The full byte array remains in managed memory until this request completes.</remarks>
    public Task<PdfBitmap> RenderPageAsync(
        byte[] pdfBytes,
        int pageIndex,
        PdfImageConversionOptions? options = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        return SubmitRender(CreateByteSource(pdfBytes), pageIndex, options, password, cancellationToken);
    }

    /// <summary>
    /// Queues a zero-based page from a PDF stream and returns an independently owned bitmap.
    /// </summary>
    /// <param name="pdfStream">Readable stream that must remain usable and unmodified until completion.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="options">Optional rendering and color-conversion settings, snapshotted during submission.</param>
    /// <param name="leaveOpen">Whether to leave the PDF stream open after request completion.</param>
    /// <param name="password">Optional document password.</param>
    /// <param name="cancellationToken">Cancels queue waiting or work that has not entered an uninterruptible stage.</param>
    /// <returns>A task that produces a caller-owned BGRA bitmap.</returns>
    /// <remarks>Non-seekable streams are buffered because PDFium requires random access.</remarks>
    public Task<PdfBitmap> RenderPageAsync(
        Stream pdfStream,
        int pageIndex,
        PdfImageConversionOptions? options = null,
        bool leaveOpen = false,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        return SubmitRender(CreateStreamSource(pdfStream, leaveOpen), pageIndex, options, password, cancellationToken);
    }

    /// <summary>
    /// Queues a zero-based page from a PDF file and saves it to an image file.
    /// </summary>
    /// <param name="pdfPath">PDF path opened when this request reaches the PDFium stage.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imagePath">Destination image path opened during the encoding stage.</param>
    /// <param name="options">Optional rendering, format, and encoding settings, snapshotted during submission.</param>
    /// <param name="password">Optional document password.</param>
    /// <param name="cancellationToken">Cancels queue waiting or work that has not entered an uninterruptible stage.</param>
    /// <returns>A task that completes after the image has been written.</returns>
    public Task SavePageAsync(
        string pdfPath,
        int pageIndex,
        string imagePath,
        PdfImageConversionOptions? options = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        return SubmitSave(CreatePathSource(pdfPath), pageIndex, CreatePathTarget(imagePath), options, password,
            cancellationToken);
    }

    /// <summary>
    /// Queues a zero-based page from a PDF file and writes it to a caller-owned image stream.
    /// </summary>
    /// <param name="pdfPath">PDF path opened when this request reaches the PDFium stage.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imageStream">Writable destination stream, which remains open.</param>
    /// <param name="options">Optional rendering, format, and encoding settings, snapshotted during submission.</param>
    /// <param name="password">Optional document password.</param>
    /// <param name="cancellationToken">Cancels queue waiting or work that has not entered an uninterruptible stage.</param>
    /// <returns>A task that completes after the image has been written.</returns>
    public Task SavePageAsync(
        string pdfPath,
        int pageIndex,
        Stream imageStream,
        PdfImageConversionOptions? options = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        return SubmitSave(CreatePathSource(pdfPath), pageIndex, CreateStreamTarget(imageStream), options, password,
            cancellationToken);
    }

    /// <summary>
    /// Queues a zero-based page from PDF bytes and saves it to an image file.
    /// </summary>
    /// <param name="pdfBytes">PDF bytes that must not be modified until completion.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imagePath">Destination image path opened during the encoding stage.</param>
    /// <param name="options">Optional rendering, format, and encoding settings, snapshotted during submission.</param>
    /// <param name="password">Optional document password.</param>
    /// <param name="cancellationToken">Cancels queue waiting or work that has not entered an uninterruptible stage.</param>
    /// <returns>A task that completes after the image has been written.</returns>
    /// <remarks>The full byte array remains in managed memory until this request completes.</remarks>
    public Task SavePageAsync(
        byte[] pdfBytes,
        int pageIndex,
        string imagePath,
        PdfImageConversionOptions? options = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        return SubmitSave(CreateByteSource(pdfBytes), pageIndex, CreatePathTarget(imagePath), options, password,
            cancellationToken);
    }

    /// <summary>
    /// Queues a zero-based page from PDF bytes and writes it to a caller-owned image stream.
    /// </summary>
    /// <param name="pdfBytes">PDF bytes that must not be modified until completion.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imageStream">Writable destination stream, which remains open.</param>
    /// <param name="options">Optional rendering, format, and encoding settings, snapshotted during submission.</param>
    /// <param name="password">Optional document password.</param>
    /// <param name="cancellationToken">Cancels queue waiting or work that has not entered an uninterruptible stage.</param>
    /// <returns>A task that completes after the image has been written.</returns>
    /// <remarks>The full byte array remains in managed memory until this request completes.</remarks>
    public Task SavePageAsync(
        byte[] pdfBytes,
        int pageIndex,
        Stream imageStream,
        PdfImageConversionOptions? options = null,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        return SubmitSave(CreateByteSource(pdfBytes), pageIndex, CreateStreamTarget(imageStream), options, password,
            cancellationToken);
    }

    /// <summary>
    /// Queues a zero-based page from a PDF stream and saves it to an image file.
    /// </summary>
    /// <param name="pdfStream">Readable stream that must remain usable and unmodified until completion.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imagePath">Destination image path opened during the encoding stage.</param>
    /// <param name="options">Optional rendering, format, and encoding settings, snapshotted during submission.</param>
    /// <param name="leaveOpen">Whether to leave the PDF stream open after request completion.</param>
    /// <param name="password">Optional document password.</param>
    /// <param name="cancellationToken">Cancels queue waiting or work that has not entered an uninterruptible stage.</param>
    /// <returns>A task that completes after the image has been written.</returns>
    /// <remarks>Non-seekable streams are buffered because PDFium requires random access.</remarks>
    public Task SavePageAsync(
        Stream pdfStream,
        int pageIndex,
        string imagePath,
        PdfImageConversionOptions? options = null,
        bool leaveOpen = false,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        return SubmitSave(CreateStreamSource(pdfStream, leaveOpen), pageIndex, CreatePathTarget(imagePath), options,
            password, cancellationToken);
    }

    /// <summary>
    /// Queues a zero-based page from a PDF stream and writes it to a separate caller-owned image stream.
    /// </summary>
    /// <param name="pdfStream">Readable stream that must remain usable and unmodified until completion.</param>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <param name="imageStream">Writable destination stream, which remains open.</param>
    /// <param name="options">Optional rendering, format, and encoding settings, snapshotted during submission.</param>
    /// <param name="leaveOpen">Whether to leave the PDF stream open after request completion.</param>
    /// <param name="password">Optional document password.</param>
    /// <param name="cancellationToken">Cancels queue waiting or work that has not entered an uninterruptible stage.</param>
    /// <returns>A task that completes after the image has been written.</returns>
    /// <remarks>Input and output must be different streams. Non-seekable PDF streams are buffered.</remarks>
    public Task SavePageAsync(
        Stream pdfStream,
        int pageIndex,
        Stream imageStream,
        PdfImageConversionOptions? options = null,
        bool leaveOpen = false,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(pdfStream, imageStream))
        {
            throw new ArgumentException("PDF input and image output must be different streams.", nameof(imageStream));
        }

        return SubmitSave(CreateStreamSource(pdfStream, leaveOpen), pageIndex, CreateStreamTarget(imageStream), options,
            password, cancellationToken);
    }

    /// <summary>
    /// Stops accepting submissions and asynchronously waits for all accepted requests to finish.
    /// </summary>
    /// <returns>A task that completes after both pipeline stages have drained and native resources are released.</returns>
    public Task CompleteAsync()
    {
        BeginShutdown(cancel: false);
        return _completion;
    }

    /// <summary>
    /// Stops accepting submissions, cancels queued requests, and waits for active uninterruptible stages to finish.
    /// </summary>
    /// <returns>A task that completes after workers stop and native resources are released.</returns>
    /// <remarks>PDFium or image-encoder calls already executing cannot be interrupted.</remarks>
    public Task CancelAsync()
    {
        BeginShutdown(cancel: true);
        return _completion;
    }

    /// <summary>
    /// Cancels queued requests, waits for active uninterruptible stages, and releases dispatcher resources.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        BeginShutdown(cancel: true);
        try
        {
            _completion.GetAwaiter().GetResult();
        }
        finally
        {
            _encodingSlots.Dispose();
        }
    }

    private Task<PdfBitmap> SubmitRender(
        PdfSource source,
        int pageIndex,
        PdfImageConversionOptions? options,
        string? password,
        CancellationToken cancellationToken)
    {
        ValidatePageIndex(pageIndex);
        var job = new BitmapRenderJob(source, pageIndex, SnapshotOptions(options), password, cancellationToken);
        return SubmitAsync(job);
    }

    private Task SubmitSave(
        PdfSource source,
        int pageIndex,
        ImageTarget target,
        PdfImageConversionOptions? options,
        string? password,
        CancellationToken cancellationToken)
    {
        ValidatePageIndex(pageIndex);
        var job = new SaveRenderJob(source, pageIndex, SnapshotOptions(options), password, cancellationToken, target);
        return SubmitAsync(job);
    }

    private async Task<PdfBitmap> SubmitAsync(BitmapRenderJob job)
    {
        await EnqueueAsync(job).ConfigureAwait(false);
        return await job.Task.ConfigureAwait(false);
    }

    private async Task SubmitAsync(SaveRenderJob job)
    {
        await EnqueueAsync(job).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    private async Task EnqueueAsync(RenderJob job)
    {
        ThrowIfNotAccepting();
        job.CancellationToken.ThrowIfCancellationRequested();

        if (_queueFullMode == PdfRenderQueueFullMode.Reject)
        {
            if (_renderQueue.Writer.TryWrite(job))
            {
                return;
            }

            ThrowIfNotAccepting();
            throw new PdfRenderQueueFullException();
        }

        try
        {
            await _renderQueue.Writer.WriteAsync(job, job.CancellationToken).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            ThrowIfNotAccepting();
            throw;
        }
    }

    private async Task ProcessRenderQueueAsync()
    {
        Exception? fatalError = null;
        try
        {
            while (await _renderQueue.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_renderQueue.Reader.TryRead(out var job))
                {
                    if (ShouldCancel(job))
                    {
                        job.Cancel();
                        continue;
                    }

                    try
                    {
                        if (job is BitmapRenderJob bitmapJob)
                        {
                            ProcessBitmapJob(bitmapJob);
                        }
                        else
                        {
                            await ProcessSaveJobAsync((SaveRenderJob)job).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (job.CancellationToken.IsCancellationRequested)
                    {
                        job.Cancel();
                    }
                    catch (Exception exception)
                    {
                        job.Fail(exception);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            fatalError = exception;
            Interlocked.Exchange(ref _accepting, 0);
            _renderQueue.Writer.TryComplete(exception);
            throw;
        }
        finally
        {
            while (_renderQueue.Reader.TryRead(out var pendingJob))
            {
                if (Volatile.Read(ref _cancelRequested) != 0)
                {
                    pendingJob.Cancel();
                }
                else
                {
                    pendingJob.Fail(fatalError ?? new InvalidOperationException("The PDF render worker stopped."));
                }
            }

            _encodingQueue.Writer.TryComplete(fatalError);
        }
    }

    private void ProcessBitmapJob(BitmapRenderJob job)
    {
        try
        {
            using var document = job.OpenDocument();
            using var page = document.LoadPage(job.PageIndex);
            var renderOptions = PdfImageConverter.GetRenderOptions(job.Options);
            var bitmap = page.Render(renderOptions);
            PdfImageConverter.ApplyConversionColorMode(bitmap, job.Options);

            if (ShouldCancel(job))
            {
                job.Cancel();
                return;
            }

            job.Complete(bitmap);
        }
        finally
        {
            job.CleanupInput();
        }
    }

    private async Task ProcessSaveJobAsync(SaveRenderJob job)
    {
        var slotAcquired = false;
        PdfNativeBitmapLease? lease = null;
        try
        {
            await _encodingSlots.WaitAsync(job.CancellationToken).ConfigureAwait(false);
            slotAcquired = true;

            if (ShouldCancel(job))
            {
                job.Cancel();
                return;
            }

            using (var document = job.OpenDocument())
            using (var page = document.LoadPage(job.PageIndex))
            {
                var renderOptions = PdfImageConverter.GetRenderOptions(job.Options);
                var (width, height) = renderOptions.GetPixelSize(page.Width, page.Height);
                lease = PdfNativeBitmapLease.Create(width, height);
                PdfImageConverter.RenderToLease(page, lease, renderOptions, job.Options);
            }

            if (ShouldCancel(job))
            {
                job.Cancel();
                return;
            }

            if (!_encodingQueue.Writer.TryWrite(new EncodingWork(job, lease)))
            {
                throw new InvalidOperationException("The PDF image encoding worker stopped.");
            }

            lease = null;
            slotAcquired = false;
        }
        finally
        {
            job.CleanupInput();
            lease?.Dispose();
            if (slotAcquired)
            {
                _encodingSlots.Release();
            }
        }
    }

    private async Task ProcessEncodingQueueAsync()
    {
        while (await _encodingQueue.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (_encodingQueue.Reader.TryRead(out var work))
            {
                Exception? error = null;
                var canceled = false;
                try
                {
                    if (ShouldCancel(work.Job))
                    {
                        canceled = true;
                    }
                    else
                    {
                        work.Job.Target.Write(work.Lease, work.Job.Options);
                    }
                }
                catch (Exception exception)
                {
                    error = exception;
                }
                finally
                {
                    try
                    {
                        work.Lease.Dispose();
                    }
                    catch (Exception exception)
                    {
                        error ??= exception;
                    }
                    finally
                    {
                        _encodingSlots.Release();
                    }
                }

                if (error is not null)
                {
                    work.Job.Fail(error);
                }
                else if (canceled)
                {
                    work.Job.Cancel();
                }
                else
                {
                    work.Job.Complete();
                }
            }
        }
    }

    private async Task CompletePipelineAsync()
    {
        try
        {
            var allWorkers = new Task[_encodingWorkers.Length + 1];
            allWorkers[0] = _nativeWorker;
            Array.Copy(_encodingWorkers, 0, allWorkers, 1, _encodingWorkers.Length);
            await Task.WhenAll(allWorkers).ConfigureAwait(false);
        }
        finally
        {
            _library.Dispose();
        }
    }

    private void BeginShutdown(bool cancel)
    {
        if (cancel)
        {
            Volatile.Write(ref _cancelRequested, 1);
        }

        if (Interlocked.Exchange(ref _accepting, 0) != 0)
        {
            _renderQueue.Writer.TryComplete();
        }
    }

    private bool ShouldCancel(RenderJob job)
    {
        return Volatile.Read(ref _cancelRequested) != 0 || job.CancellationToken.IsCancellationRequested;
    }

    private void ThrowIfNotAccepting()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (Volatile.Read(ref _accepting) == 0)
        {
            throw new InvalidOperationException("The PDF render dispatcher is no longer accepting requests.");
        }
    }

    private static void ValidatePageIndex(int pageIndex)
    {
        if (pageIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex,
                "Page index must be zero or greater.");
        }
    }

    private static PdfSource CreatePathSource(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            throw new ArgumentException("PDF path cannot be null or whitespace.", nameof(pdfPath));
        }

        return new PathPdfSource(pdfPath);
    }

    private static PdfSource CreateByteSource(byte[] pdfBytes)
    {
        if (pdfBytes is null)
        {
            throw new ArgumentNullException(nameof(pdfBytes));
        }

        if (pdfBytes.Length == 0)
        {
            throw new ArgumentException("PDF bytes cannot be empty.", nameof(pdfBytes));
        }

        return new BytePdfSource(pdfBytes);
    }

    private static PdfSource CreateStreamSource(Stream pdfStream, bool leaveOpen)
    {
        if (pdfStream is null)
        {
            throw new ArgumentNullException(nameof(pdfStream));
        }

        if (!pdfStream.CanRead)
        {
            throw new ArgumentException("PDF stream must be readable.", nameof(pdfStream));
        }

        return new StreamPdfSource(pdfStream, leaveOpen);
    }

    private static ImageTarget CreatePathTarget(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path cannot be null or whitespace.", nameof(imagePath));
        }

        return new PathImageTarget(imagePath);
    }

    private static ImageTarget CreateStreamTarget(Stream imageStream)
    {
        if (imageStream is null)
        {
            throw new ArgumentNullException(nameof(imageStream));
        }

        if (!imageStream.CanWrite)
        {
            throw new ArgumentException("Image stream must be writable.", nameof(imageStream));
        }

        return new StreamImageTarget(imageStream);
    }

    private static PdfImageConversionOptions SnapshotOptions(PdfImageConversionOptions? options)
    {
        options ??= new PdfImageConversionOptions();
        var sourceRender = options.Render ?? new PdfPageRenderOptions();
        var sourceEncoding = options.Encoding ?? new PdfImageEncodingOptions();

        if (!Enum.IsDefined(typeof(PdfImageOutputFormat), options.Format))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Format,
                "Image format must be a defined value.");
        }

        if (!Enum.IsDefined(typeof(PdfImageColorMode), options.ColorMode))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.ColorMode,
                "Image color mode must be a defined value.");
        }

        return new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions
            {
                Dpi = sourceRender.Dpi,
                Scale = sourceRender.Scale,
                Rotation = sourceRender.Rotation,
                Flags = sourceRender.Flags,
                Width = sourceRender.Width,
                Height = sourceRender.Height,
                WithAspectRatio = sourceRender.WithAspectRatio,
                AntiAliasing = sourceRender.AntiAliasing,
                BackgroundColor = sourceRender.BackgroundColor,
                FillBackground = sourceRender.FillBackground,
            },
            Format = options.Format,
            Encoding = new PdfImageEncodingOptions
            {
                Quality = sourceEncoding.Quality,
                PngCompressionLevel = sourceEncoding.PngCompressionLevel,
            },
            ColorMode = options.ColorMode,
            BlackAndWhiteThreshold = options.BlackAndWhiteThreshold,
        };
    }

    private abstract class RenderJob
    {
        private readonly PdfSource _source;

        protected RenderJob(
            PdfSource source,
            int pageIndex,
            PdfImageConversionOptions options,
            string? password,
            CancellationToken cancellationToken)
        {
            _source = source;
            PageIndex = pageIndex;
            Options = options;
            Password = password;
            CancellationToken = cancellationToken;
        }

        internal int PageIndex { get; }
        internal PdfImageConversionOptions Options { get; }
        internal string? Password { get; }
        internal CancellationToken CancellationToken { get; }

        internal PdfDocument OpenDocument()
        {
            return _source.Open(Password);
        }

        internal void CleanupInput()
        {
            _source.CleanupIfNotOpened();
        }

        internal abstract void Cancel();
        internal abstract void Fail(Exception exception);
    }

    private sealed class BitmapRenderJob : RenderJob
    {
        private readonly TaskCompletionSource<PdfBitmap> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal BitmapRenderJob(
            PdfSource source,
            int pageIndex,
            PdfImageConversionOptions options,
            string? password,
            CancellationToken cancellationToken)
            : base(source, pageIndex, options, password, cancellationToken)
        {
        }

        internal Task<PdfBitmap> Task => _completion.Task;

        internal void Complete(PdfBitmap bitmap)
        {
            _completion.TrySetResult(bitmap);
        }

        internal override void Cancel()
        {
            CleanupInput();
            _completion.TrySetCanceled();
        }

        internal override void Fail(Exception exception)
        {
            CleanupInput();
            _completion.TrySetException(exception);
        }
    }

    private sealed class SaveRenderJob : RenderJob
    {
        private readonly TaskCompletionSource<object?> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal SaveRenderJob(
            PdfSource source,
            int pageIndex,
            PdfImageConversionOptions options,
            string? password,
            CancellationToken cancellationToken,
            ImageTarget target)
            : base(source, pageIndex, options, password, cancellationToken)
        {
            Target = target;
        }

        internal Task Task => _completion.Task;
        internal ImageTarget Target { get; }

        internal void Complete()
        {
            _completion.TrySetResult(null);
        }

        internal override void Cancel()
        {
            CleanupInput();
            _completion.TrySetCanceled();
        }

        internal override void Fail(Exception exception)
        {
            CleanupInput();
            _completion.TrySetException(exception);
        }
    }

    private abstract class PdfSource
    {
        internal abstract PdfDocument Open(string? password);
        internal virtual void CleanupIfNotOpened()
        {
        }
    }

    private sealed class PathPdfSource : PdfSource
    {
        private readonly string _path;

        internal PathPdfSource(string path)
        {
            _path = path;
        }

        internal override PdfDocument Open(string? password)
        {
            return PdfDocument.Load(_path, password);
        }
    }

    private sealed class BytePdfSource : PdfSource
    {
        private readonly byte[] _bytes;

        internal BytePdfSource(byte[] bytes)
        {
            _bytes = bytes;
        }

        internal override PdfDocument Open(string? password)
        {
            return PdfDocument.Load(_bytes, password);
        }
    }

    private sealed class StreamPdfSource : PdfSource
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private int _opened;
        private int _cleaned;

        internal StreamPdfSource(Stream stream, bool leaveOpen)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
        }

        internal override PdfDocument Open(string? password)
        {
            var document = PdfDocument.Load(_stream, _leaveOpen, password);
            Volatile.Write(ref _opened, 1);
            return document;
        }

        internal override void CleanupIfNotOpened()
        {
            if (!_leaveOpen && Volatile.Read(ref _opened) == 0 && Interlocked.Exchange(ref _cleaned, 1) == 0)
            {
                _stream.Dispose();
            }
        }
    }

    private abstract class ImageTarget
    {
        internal abstract void Write(PdfNativeBitmapLease bitmap, PdfImageConversionOptions options);
    }

    private sealed class PathImageTarget : ImageTarget
    {
        private readonly string _path;

        internal PathImageTarget(string path)
        {
            _path = path;
        }

        internal override void Write(PdfNativeBitmapLease bitmap, PdfImageConversionOptions options)
        {
            PdfImageConverter.SaveBitmap(bitmap, _path, options.Format, options.Encoding);
        }
    }

    private sealed class StreamImageTarget : ImageTarget
    {
        private readonly Stream _stream;

        internal StreamImageTarget(Stream stream)
        {
            _stream = stream;
        }

        internal override void Write(PdfNativeBitmapLease bitmap, PdfImageConversionOptions options)
        {
            PdfImageConverter.SaveBitmap(bitmap, _stream, options.Format, options.Encoding);
        }
    }

    private sealed class EncodingWork
    {
        internal EncodingWork(SaveRenderJob job, PdfNativeBitmapLease lease)
        {
            Job = job;
            Lease = lease;
        }

        internal SaveRenderJob Job { get; }
        internal PdfNativeBitmapLease Lease { get; }
    }
}
