using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfDocumentTests
{
    [Fact]
    public void Non_seekable_stream_uses_buffer_after_owned_source_is_disposed()
    {
        var bytes = File.ReadAllBytes(GetTestPdfPath("smoke.pdf"));
        var source = new TestNonSeekableReadStream(bytes);
        using var pdfium = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(source);

        Assert.True(source.IsDisposed);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var page = document.LoadPage(0);
        var bitmap = page.Render(new PdfPageRenderOptions { Dpi = 72 });

        Assert.Contains(bitmap.Pixels, pixel => pixel != 0);
    }

    [Fact]
    public void Non_seekable_stream_respects_leave_open()
    {
        var bytes = File.ReadAllBytes(GetTestPdfPath("smoke.pdf"));
        using var source = new TestNonSeekableReadStream(bytes);
        using var pdfium = PdfiumLibrary.Initialize();

        using (var document = PdfDocument.Load(source, leaveOpen: true))
        {
            Assert.Equal(1, document.PageCount);
            Assert.False(source.IsDisposed);
        }

        Assert.False(source.IsDisposed);
    }

    [Fact]
    public void Empty_non_seekable_stream_is_rejected_and_owned_source_is_disposed()
    {
        var source = new TestNonSeekableReadStream([]);
        using var pdfium = PdfiumLibrary.Initialize();

        Assert.Throws<ArgumentException>(() => PdfDocument.Load(source));
        Assert.True(source.IsDisposed);
    }

    [Fact]
    public void Invalid_non_seekable_stream_is_rejected_and_owned_source_is_disposed()
    {
        var source = new TestNonSeekableReadStream([1, 2, 3]);
        using var pdfium = PdfiumLibrary.Initialize();

        Assert.Throws<PdfiumException>(() => PdfDocument.Load(source));
        Assert.True(source.IsDisposed);
    }

    private static string GetTestPdfPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestAssets", fileName);
    }
}

internal sealed class TestNonSeekableReadStream : Stream
{
    private readonly MemoryStream _inner;

    internal TestNonSeekableReadStream(byte[] bytes)
    {
        _inner = new MemoryStream(bytes, writable: false);
    }

    internal bool IsDisposed { get; private set; }

    public override bool CanRead => !IsDisposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            IsDisposed = true;
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
