namespace PdfiumRaster;

/// <summary>
/// Specifies how a render dispatcher handles submissions when its bounded queue is full.
/// </summary>
public enum PdfRenderQueueFullMode
{
    /// <summary>
    /// Waits asynchronously for queue capacity. The submission cancellation token can cancel this wait.
    /// </summary>
    Wait,

    /// <summary>
    /// Rejects the submission immediately with a <see cref="PdfRenderQueueFullException" />.
    /// </summary>
    Reject,
}
