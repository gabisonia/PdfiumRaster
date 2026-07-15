using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfRenderDispatcherTests
{
    [Fact]
    public async Task Concurrent_path_bytes_and_stream_requests_render_owned_bitmaps()
    {
        var path = GetTestPdfPath("smoke.pdf");
        var bytes = File.ReadAllBytes(path);
        using var input = new MemoryStream(bytes, writable: false);
        using var dispatcher = new PdfRenderDispatcher();

        var tasks = new[]
        {
            dispatcher.RenderPageAsync(path, 0, PreviewOptions()),
            dispatcher.RenderPageAsync(bytes, 0, PreviewOptions()),
            dispatcher.RenderPageAsync(input, 0, PreviewOptions(), leaveOpen: true),
        };

        var bitmaps = await Task.WhenAll(tasks);

        Assert.All(bitmaps, bitmap =>
        {
            Assert.True(bitmap.Width > 0);
            Assert.True(bitmap.Height > 0);
            Assert.Contains(bitmap.Pixels, pixel => pixel != 0);
        });
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task Save_overloads_write_files_and_caller_owned_streams()
    {
        var pdfPath = GetTestPdfPath("smoke.pdf");
        var pdfBytes = File.ReadAllBytes(pdfPath);
        var imagePath = Path.Combine(AppContext.BaseDirectory, "TestOutput", $"dispatcher-{Guid.NewGuid():N}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);

        try
        {
            using var byteOutput = new MemoryStream();
            using var streamInput = new MemoryStream(pdfBytes, writable: false);
            using var streamOutput = new MemoryStream();
            using var dispatcher = new PdfRenderDispatcher();
            var options = PngOptions();

            await Task.WhenAll(
                dispatcher.SavePageAsync(pdfPath, 0, imagePath, options),
                dispatcher.SavePageAsync(pdfBytes, 0, byteOutput, options),
                dispatcher.SavePageAsync(streamInput, 0, streamOutput, options, leaveOpen: true));

            Assert.True(new FileInfo(imagePath).Length > 0);
            AssertPng(byteOutput.ToArray());
            AssertPng(streamOutput.ToArray());
            byteOutput.WriteByte(0);
            streamOutput.WriteByte(0);
            Assert.True(streamInput.CanRead);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task Failed_request_does_not_stop_dispatcher()
    {
        using var dispatcher = new PdfRenderDispatcher();

        var failed = dispatcher.RenderPageAsync(new byte[] { 1, 2, 3 }, 0, PreviewOptions());
        var successful = dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions());

        await Assert.ThrowsAsync<PdfiumException>(() => failed);
        var bitmap = await successful;

        Assert.True(bitmap.Width > 0);
    }

    [Fact]
    public async Task Submission_snapshots_nested_options()
    {
        var pdfBytes = File.ReadAllBytes(GetTestPdfPath("smoke.pdf"));
        using var blocker = new BlockingReadStream(pdfBytes);
        using var dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions { QueueCapacity = 2 });
        var expected = PdfImageConverter.RenderPage(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions());
        var options = PreviewOptions();

        var blockingTask = dispatcher.RenderPageAsync(blocker, 0, PreviewOptions(), leaveOpen: true);
        blocker.WaitUntilReadStarted();
        var queuedTask = dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, options);
        options.Render.Dpi = 144;
        options.Render.Width = 100;
        blocker.Release();

        await blockingTask;
        var actual = await queuedTask;

        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
    }

    [Fact]
    public async Task Reject_mode_reports_full_queue()
    {
        var pdfBytes = File.ReadAllBytes(GetTestPdfPath("smoke.pdf"));
        using var blocker = new BlockingReadStream(pdfBytes);
        using var dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions
        {
            QueueCapacity = 1,
            QueueFullMode = PdfRenderQueueFullMode.Reject,
        });

        var active = dispatcher.RenderPageAsync(blocker, 0, PreviewOptions(), leaveOpen: true);
        blocker.WaitUntilReadStarted();
        var queued = dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions());
        var rejected = dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions());

        await Assert.ThrowsAsync<PdfRenderQueueFullException>(() => rejected);
        blocker.Release();
        await Task.WhenAll(active, queued);
    }

    [Fact]
    public async Task Wait_mode_applies_asynchronous_backpressure()
    {
        var pdfBytes = File.ReadAllBytes(GetTestPdfPath("smoke.pdf"));
        using var blocker = new BlockingReadStream(pdfBytes);
        using var dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions { QueueCapacity = 1 });

        var active = dispatcher.RenderPageAsync(blocker, 0, PreviewOptions(), leaveOpen: true);
        blocker.WaitUntilReadStarted();
        var queued = dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions());
        var waiting = dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions());

        Assert.False(waiting.IsCompleted);
        blocker.Release();
        await Task.WhenAll(active, queued, waiting);
    }

    [Fact]
    public async Task Queued_cancellation_disposes_owned_input_stream()
    {
        var pdfBytes = File.ReadAllBytes(GetTestPdfPath("smoke.pdf"));
        using var blocker = new BlockingReadStream(pdfBytes);
        var ownedInput = new TrackingMemoryStream(pdfBytes);
        using var cancellation = new CancellationTokenSource();
        using var dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions { QueueCapacity = 1 });

        var active = dispatcher.RenderPageAsync(blocker, 0, PreviewOptions(), leaveOpen: true);
        blocker.WaitUntilReadStarted();
        var queued = dispatcher.RenderPageAsync(ownedInput, 0, PreviewOptions(), cancellationToken: cancellation.Token);
        cancellation.Cancel();
        blocker.Release();

        await active;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.True(ownedInput.IsDisposed);
    }

    [Fact]
    public async Task Encoding_workers_can_write_two_images_concurrently()
    {
        using var firstOutput = new BlockingWriteStream();
        using var secondOutput = new BlockingWriteStream();
        using var dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions
        {
            EncodingConcurrency = 2,
            QueueCapacity = 2,
        });
        var options = new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Width = 128, Height = 128 },
            Format = PdfImageOutputFormat.Bmp,
        };

        var first = dispatcher.SavePageAsync(GetTestPdfPath("smoke.pdf"), 0, firstOutput, options);
        var second = dispatcher.SavePageAsync(GetTestPdfPath("smoke.pdf"), 0, secondOutput, options);

        firstOutput.WaitUntilWriteStarted();
        secondOutput.WaitUntilWriteStarted();
        firstOutput.Release();
        secondOutput.Release();
        await Task.WhenAll(first, second);

        Assert.True(firstOutput.Length > 0);
        Assert.True(secondOutput.Length > 0);
    }

    [Fact]
    public async Task CompleteAsync_drains_jobs_and_stops_submissions()
    {
        using var dispatcher = new PdfRenderDispatcher();
        var first = dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions());
        var second = dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions());

        await dispatcher.CompleteAsync();

        Assert.True(first.IsCompletedSuccessfully);
        Assert.True(second.IsCompletedSuccessfully);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions()));
    }

    [Fact]
    public async Task CancelAsync_cancels_active_result_and_queued_jobs_at_stage_boundaries()
    {
        var pdfBytes = File.ReadAllBytes(GetTestPdfPath("smoke.pdf"));
        using var blocker = new BlockingReadStream(pdfBytes);
        using var dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions { QueueCapacity = 1 });

        var active = dispatcher.RenderPageAsync(blocker, 0, PreviewOptions(), leaveOpen: true);
        blocker.WaitUntilReadStarted();
        var queued = dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions());
        var cancel = dispatcher.CancelAsync();
        blocker.Release();

        await cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
    }

    [Fact]
    public async Task Disposed_dispatcher_rejects_new_requests()
    {
        var dispatcher = new PdfRenderDispatcher();
        dispatcher.Dispose();
        dispatcher.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            dispatcher.RenderPageAsync(GetTestPdfPath("smoke.pdf"), 0, PreviewOptions()));
    }

    [Fact]
    public void Options_validate_capacity_concurrency_and_mode()
    {
        var options = new PdfRenderDispatcherOptions();

        Assert.Equal(42, options.QueueCapacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.QueueCapacity = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.EncodingConcurrency = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.QueueFullMode = (PdfRenderQueueFullMode)99);
    }

    [Fact]
    public void Same_input_and_output_stream_is_rejected()
    {
        using var stream = new MemoryStream(File.ReadAllBytes(GetTestPdfPath("smoke.pdf")));
        using var dispatcher = new PdfRenderDispatcher();

        var exception = Record.Exception((Action)(() => _ = dispatcher.SavePageAsync(stream, 0, stream)));

        Assert.IsType<ArgumentException>(exception);
    }

    private static PdfImageConversionOptions PreviewOptions()
    {
        return new PdfImageConversionOptions { Render = PdfPageRenderOptions.ScreenPreview };
    }

    private static PdfImageConversionOptions PngOptions()
    {
        return new PdfImageConversionOptions
        {
            Render = PdfPageRenderOptions.ScreenPreview,
            Format = PdfImageOutputFormat.Png,
            Encoding = PdfImageEncodingOptions.Fast,
        };
    }

    private static void AssertPng(byte[] bytes)
    {
        Assert.True(bytes.Length > 4);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    private static string GetTestPdfPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestAssets", fileName);
    }

    private sealed class TrackingMemoryStream : MemoryStream
    {
        internal TrackingMemoryStream(byte[] bytes)
            : base(bytes, writable: false)
        {
        }

        internal bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingReadStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly ManualResetEventSlim _readStarted = new();
        private readonly ManualResetEventSlim _release = new();
        private int _blocked;

        internal BlockingReadStream(byte[] bytes)
        {
            _inner = new MemoryStream(bytes, writable: false);
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        internal void WaitUntilReadStarted()
        {
            Assert.True(_readStarted.Wait(TimeSpan.FromSeconds(10)), "PDF read did not start.");
        }

        internal void Release()
        {
            _release.Set();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Interlocked.Exchange(ref _blocked, 1) == 0)
            {
                _readStarted.Set();
                if (!_release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Timed out waiting to release the PDF input stream.");
                }
            }

            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void Flush() => _inner.Flush();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _release.Set();
                _readStarted.Dispose();
                _release.Dispose();
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class BlockingWriteStream : MemoryStream
    {
        private readonly ManualResetEventSlim _writeStarted = new();
        private readonly ManualResetEventSlim _release = new();
        private int _blocked;

        internal void WaitUntilWriteStarted()
        {
            Assert.True(_writeStarted.Wait(TimeSpan.FromSeconds(10)), "Image write did not start.");
        }

        internal void Release()
        {
            _release.Set();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Interlocked.Exchange(ref _blocked, 1) == 0)
            {
                _writeStarted.Set();
                if (!_release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Timed out waiting to release the image output stream.");
                }
            }

            base.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _release.Set();
                _writeStarted.Dispose();
                _release.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
