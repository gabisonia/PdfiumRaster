namespace PdfiumRaster;

/// <summary>
/// Configures bounded concurrent request handling for <see cref="PdfRenderDispatcher" />.
/// </summary>
public sealed class PdfRenderDispatcherOptions
{
    private int _queueCapacity = 42;
    private int _encodingConcurrency = Math.Max(1, Math.Min(2, Environment.ProcessorCount));
    private PdfRenderQueueFullMode _queueFullMode;

    /// <summary>
    /// Gets or sets the maximum number of accepted requests waiting for the serialized PDFium stage. The default is 42.
    /// </summary>
    /// <remarks>
    /// This limit bounds queued request descriptors and references to caller-provided PDF data. Rendered bitmap memory
    /// is bounded separately by <see cref="EncodingConcurrency" />.
    /// </remarks>
    public int QueueCapacity
    {
        get => _queueCapacity;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Queue capacity must be greater than zero.");
            }

            _queueCapacity = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of image encodes that may run concurrently.
    /// </summary>
    /// <remarks>
    /// The default is the smaller of two and the logical processor count, with a minimum of one. Each concurrent encode
    /// may retain one full rendered page bitmap.
    /// </remarks>
    public int EncodingConcurrency
    {
        get => _encodingConcurrency;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Encoding concurrency must be greater than zero.");
            }

            _encodingConcurrency = value;
        }
    }

    /// <summary>
    /// Gets or sets how submissions behave when the bounded queue is full. The default is
    /// <see cref="PdfRenderQueueFullMode.Wait" />.
    /// </summary>
    public PdfRenderQueueFullMode QueueFullMode
    {
        get => _queueFullMode;
        set
        {
            if (!Enum.IsDefined(typeof(PdfRenderQueueFullMode), value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "Queue full mode must be a defined value.");
            }

            _queueFullMode = value;
        }
    }
}
