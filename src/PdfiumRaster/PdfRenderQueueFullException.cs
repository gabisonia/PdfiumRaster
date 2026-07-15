namespace PdfiumRaster;

/// <summary>
/// The exception thrown when a render dispatcher configured to reject work has no queue capacity available.
/// </summary>
public sealed class PdfRenderQueueFullException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRenderQueueFullException" /> class.
    /// </summary>
    public PdfRenderQueueFullException()
        : base("The PDF render queue is full.")
    {
    }
}
