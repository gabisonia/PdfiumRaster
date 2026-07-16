using System.Runtime.ExceptionServices;
using System.Threading.Channels;

namespace PdfiumRaster;

internal delegate void PdfBatchBitmapEncoder(
    PdfBitmap bitmap,
    string imagePath,
    PdfImageOutputFormat format,
    PdfImageEncodingOptions encodingOptions);

internal sealed class PdfBatchSavePipeline : IDisposable
{
    internal const int BufferCount = 2;

    private readonly PdfBatchBitmapEncoder _encoder;
    private readonly Channel<RenderSlot> _availableSlots;
    private readonly Channel<EncodingWork> _encodingQueue;
    private readonly RenderSlot[] _slots;
    private readonly Task[] _workers;
    private ExceptionDispatchInfo? _failure;
    private int _shutdownStarted;
    private int _disposed;

    internal PdfBatchSavePipeline(PdfBatchBitmapEncoder encoder)
    {
        _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        _availableSlots = Channel.CreateBounded<RenderSlot>(new BoundedChannelOptions(BufferCount)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
        _encodingQueue = Channel.CreateBounded<EncodingWork>(new BoundedChannelOptions(BufferCount)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true,
        });

        _slots = new RenderSlot[BufferCount];
        for (var index = 0; index < _slots.Length; index++)
        {
            var slot = new RenderSlot();
            _slots[index] = slot;
            _availableSlots.Writer.TryWrite(slot);
        }

        _workers = new Task[BufferCount];
        for (var index = 0; index < _workers.Length; index++)
        {
            _workers[index] = Task.Run(ProcessEncodingQueueAsync);
        }
    }

    internal RenderSlot AcquireSlot()
    {
        ThrowIfDisposed();
        ThrowIfFailed();

        var slot = _availableSlots.Reader.ReadAsync().AsTask().GetAwaiter().GetResult();
        try
        {
            ThrowIfFailed();
            return slot;
        }
        catch
        {
            ReturnSlot(slot);
            throw;
        }
    }

    internal void Queue(
        RenderSlot slot,
        string imagePath,
        PdfImageOutputFormat format,
        PdfImageEncodingOptions encodingOptions)
    {
        if (slot is null)
        {
            throw new ArgumentNullException(nameof(slot));
        }

        ThrowIfDisposed();
        _encodingQueue.Writer.WriteAsync(new EncodingWork(slot, imagePath, format, encodingOptions))
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    internal void ReturnSlot(RenderSlot slot)
    {
        if (!_availableSlots.Writer.TryWrite(slot))
        {
            throw new InvalidOperationException("The batch render slot could not be returned.");
        }
    }

    internal void Complete()
    {
        ThrowIfDisposed();
        StopWorkers();
        ThrowIfFailed();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        StopWorkers();
        foreach (var slot in _slots)
        {
            slot.Dispose();
        }
    }

    private async Task ProcessEncodingQueueAsync()
    {
        while (await _encodingQueue.Reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (_encodingQueue.Reader.TryRead(out var work))
            {
                try
                {
                    if (Volatile.Read(ref _failure) is null)
                    {
                        _encoder(work.Slot.Bitmap, work.ImagePath, work.Format, work.EncodingOptions);
                    }
                }
                catch (Exception exception)
                {
                    Interlocked.CompareExchange(
                        ref _failure,
                        ExceptionDispatchInfo.Capture(exception),
                        comparand: null);
                }
                finally
                {
                    ReturnSlot(work.Slot);
                }
            }
        }
    }

    private void StopWorkers()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) == 0)
        {
            _encodingQueue.Writer.TryComplete();
        }

        Task.WhenAll(_workers).GetAwaiter().GetResult();
    }

    private void ThrowIfFailed()
    {
        Volatile.Read(ref _failure)?.Throw();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    internal sealed class RenderSlot : IDisposable
    {
        private PdfBitmapLease? _lease;

        internal PdfBitmap Bitmap => (_lease ?? throw new InvalidOperationException(
            "The batch render slot has not been initialized.")).Bitmap;

        internal PdfBitmapLease EnsureLease(PdfPage page, PdfPageRenderOptions renderOptions)
        {
            _lease = PdfImageConverter.EnsureBitmapLease(_lease, page, renderOptions);
            return _lease;
        }

        public void Dispose()
        {
            _lease?.Dispose();
            _lease = null;
        }
    }

    private sealed class EncodingWork
    {
        internal EncodingWork(
            RenderSlot slot,
            string imagePath,
            PdfImageOutputFormat format,
            PdfImageEncodingOptions encodingOptions)
        {
            Slot = slot;
            ImagePath = imagePath;
            Format = format;
            EncodingOptions = encodingOptions;
        }

        internal RenderSlot Slot { get; }
        internal string ImagePath { get; }
        internal PdfImageOutputFormat Format { get; }
        internal PdfImageEncodingOptions EncodingOptions { get; }
    }
}
