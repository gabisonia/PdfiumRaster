using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfRenderSessionTests
{
    [Fact]
    public void Open_path_renders_owned_bitmap()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));

        var bitmap = session.RenderPage(0, PreviewOptions());

        Assert.Equal(1, session.PageCount);
        Assert.Contains(bitmap.Pixels, pixel => pixel != 0);
    }

    [Fact]
    public void Open_bytes_renders_page()
    {
        var bytes = File.ReadAllBytes(GetTestPdfPath("smoke.pdf"));
        using var session = PdfRenderSession.Open(bytes);

        var width = session.RenderPage(0, bitmap => bitmap.Width, PreviewOptions());

        Assert.True(width > 0);
    }

    [Fact]
    public void Open_seekable_stream_honors_leave_open()
    {
        using var stream = File.OpenRead(GetTestPdfPath("smoke.pdf"));

        using (var session = PdfRenderSession.Open(stream, leaveOpen: true))
        {
            Assert.Equal(1, session.PageCount);
        }

        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Open_stream_disposes_stream_by_default()
    {
        var stream = File.OpenRead(GetTestPdfPath("smoke.pdf"));

        using (var session = PdfRenderSession.Open(stream))
        {
            Assert.Equal(1, session.PageCount);
        }

        Assert.False(stream.CanRead);
    }

    [Fact]
    public void RenderPage_callback_reuses_buffer_for_matching_dimensions()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));
        PdfBitmap? first = null;
        PdfBitmap? second = null;

        session.RenderPage(0, bitmap => first = bitmap, PreviewOptions());
        session.RenderPage(0, bitmap => second = bitmap, PreviewOptions());

        Assert.Same(first, second);
    }

    [Fact]
    public void RenderPage_callback_replaces_buffer_when_dimensions_change()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));
        PdfBitmap? first = null;
        PdfBitmap? second = null;

        session.RenderPage(0, bitmap => first = bitmap, PreviewOptions());
        session.RenderPage(0, bitmap => second = bitmap, new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Dpi = 144 },
        });

        Assert.NotSame(first, second);
        Assert.NotEqual(first!.Width, second!.Width);
    }

    [Fact]
    public void RenderPageInto_reuses_caller_destination()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));
        var options = PreviewOptions();
        var size = session.RenderPage(0, bitmap => (bitmap.Width, bitmap.Height), options);
        var destination = PdfBitmap.Create(size.Width, size.Height);

        session.RenderPageInto(0, destination, options);

        Assert.Contains(destination.Pixels, pixel => pixel != 0);
    }

    [Fact]
    public void Callback_exception_propagates_and_session_remains_usable()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));

        Assert.Throws<TestCallbackException>(() =>
            session.RenderPage(0, (Action<PdfBitmap>)(_ => throw new TestCallbackException()), PreviewOptions()));

        Assert.Equal(1, session.PageCount);
        Assert.True(session.RenderPage(0, bitmap => bitmap.Width, PreviewOptions()) > 0);
    }

    [Fact]
    public void Callback_reentrant_operation_is_rejected()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));

        session.RenderPage(0, bitmap =>
        {
            Assert.NotNull(bitmap);
            Assert.Throws<InvalidOperationException>(() => { var pageCount = session.PageCount; });
        }, PreviewOptions());
    }

    [Fact]
    public void Callback_concurrent_operation_is_rejected()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));

        session.RenderPage(0, bitmap =>
        {
            Assert.NotNull(bitmap);
            var exception = Task.Run(() => Record.Exception(() => { var pageCount = session.PageCount; }))
                .GetAwaiter()
                .GetResult();
            Assert.IsType<InvalidOperationException>(exception);
        }, PreviewOptions());
    }

    [Fact]
    public void SavePage_writes_png_and_leaves_destination_open()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));
        using var stream = new MemoryStream();

        session.SavePage(0, stream, new PdfImageConversionOptions
        {
            Render = PdfPageRenderOptions.ScreenPreview,
            Format = PdfImageOutputFormat.Png,
            Encoding = PdfImageEncodingOptions.Fast,
        });

        stream.WriteByte(0);
        var bytes = stream.ToArray();
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
    }

    [Fact]
    public void Invalid_page_does_not_make_session_unusable()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));

        Assert.Throws<ArgumentOutOfRangeException>(() => session.RenderPage(1, PreviewOptions()));

        Assert.Equal(1, session.PageCount);
    }

    [Fact]
    public void Operations_after_dispose_are_rejected()
    {
        var session = PdfRenderSession.Open(GetTestPdfPath("smoke.pdf"));
        session.Dispose();
        session.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = session.PageCount);
        Assert.Throws<ObjectDisposedException>(() => session.RenderPage(0, PreviewOptions()));
    }

    private static PdfImageConversionOptions PreviewOptions()
    {
        return new PdfImageConversionOptions { Render = PdfPageRenderOptions.ScreenPreview };
    }

    private static string GetTestPdfPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestAssets", fileName);
    }

    private sealed class TestCallbackException : Exception
    {
    }
}
