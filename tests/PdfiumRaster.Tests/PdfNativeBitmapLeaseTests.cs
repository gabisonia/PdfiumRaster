using System.Runtime.InteropServices;
using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfNativeBitmapLeaseTests
{
    [Theory]
    [InlineData(PdfImageColorMode.Color)]
    [InlineData(PdfImageColorMode.Grayscale)]
    [InlineData(PdfImageColorMode.BlackAndWhite)]
    public void Native_and_managed_render_buffers_have_equivalent_pixels(PdfImageColorMode colorMode)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath());
        using var page = document.LoadPage(0);
        var options = new PdfImageConversionOptions
        {
            Render = PdfPageRenderOptions.ScreenPreview,
            ColorMode = colorMode,
            BlackAndWhiteThreshold = 137,
        };
        var renderOptions = PdfImageConverter.GetRenderOptions(options);
        var (width, height) = renderOptions.GetPixelSize(page.Width, page.Height);
        using var managed = PdfBitmapLease.Rent(width, height, clear: false);
        using var native = PdfNativeBitmapLease.Create(width, height);

        var managedBitmap = PdfImageConverter.RenderToLease(page, managed, renderOptions, options);
        PdfImageConverter.RenderToLease(page, native, renderOptions, options);

        var nativePixels = new byte[native.PixelDataSize];
        Marshal.Copy(native.Pixels, nativePixels, 0, nativePixels.Length);

        for (var y = 0; y < height; y++)
        {
            var managedRow = y * managedBitmap.Stride;
            var nativeRow = y * native.Stride;
            Assert.Equal(
                managedBitmap.Pixels.AsSpan(managedRow, width * 4).ToArray(),
                nativePixels.AsSpan(nativeRow, width * 4).ToArray());
        }
    }

    [Fact]
    public void Native_render_clears_reused_pixels_when_background_fill_is_disabled()
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath());
        using var page = document.LoadPage(0);
        var options = new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions
            {
                Width = 320,
                Height = 240,
                FillBackground = false,
            },
        };
        var renderOptions = PdfImageConverter.GetRenderOptions(options);
        using var managed = PdfBitmapLease.Rent(width: 320, height: 240, clear: false);
        using var native = PdfNativeBitmapLease.Create(width: 320, height: 240);
        var dirtyPixels = new byte[native.PixelDataSize];
        Array.Fill(dirtyPixels, (byte)0x7f);
        Marshal.Copy(dirtyPixels, 0, native.Pixels, dirtyPixels.Length);

        var managedBitmap = PdfImageConverter.RenderToLease(page, managed, renderOptions, options);
        PdfImageConverter.RenderToLease(page, native, renderOptions, options);

        var nativePixels = new byte[native.PixelDataSize];
        Marshal.Copy(native.Pixels, nativePixels, 0, nativePixels.Length);

        for (var y = 0; y < native.Height; y++)
        {
            Assert.Equal(
                managedBitmap.Pixels.AsSpan(y * managedBitmap.Stride, managedBitmap.Width * 4).ToArray(),
                nativePixels.AsSpan(y * native.Stride, native.Width * 4).ToArray());
        }
    }

    [Fact]
    public void EnsureNativeBitmapLease_reuses_matching_dimensions_and_replaces_changed_dimensions()
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath());
        using var page = document.LoadPage(0);
        PdfNativeBitmapLease? lease = null;

        try
        {
            var screen = PdfPageRenderOptions.ScreenPreview;
            lease = PdfImageConverter.EnsureNativeBitmapLease(lease, page, screen);
            var original = lease;

            lease = PdfImageConverter.EnsureNativeBitmapLease(lease, page, screen);
            Assert.Same(original, lease);

            var resized = new PdfPageRenderOptions { Width = 320, Height = 240 };
            lease = PdfImageConverter.EnsureNativeBitmapLease(lease, page, resized);

            Assert.NotSame(original, lease);
            Assert.Throws<ObjectDisposedException>(() => _ = original.Handle);
            Assert.Equal(320, lease.Width);
            Assert.Equal(240, lease.Height);
        }
        finally
        {
            lease?.Dispose();
        }
    }

    [Fact]
    public void Dispose_is_idempotent_and_rejects_buffer_access()
    {
        var lease = PdfNativeBitmapLease.Create(16, 12);

        lease.Dispose();
        lease.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = lease.Handle);
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Pixels);
    }

    [Fact]
    public void Native_bmp_writing_bounds_managed_copy_chunks()
    {
        using var bitmap = PdfNativeBitmapLease.Create(width: 1200, height: 1600);
        using var stream = new RecordingWriteStream();

        PdfImageWriter.Write(bitmap, stream, PdfImageOutputFormat.Bmp, new PdfImageEncodingOptions());

        Assert.Equal(54L + bitmap.PixelDataSize, stream.BytesWritten);
        Assert.True(stream.MaximumWriteSize <= 64 * 1024);
    }

    private static string GetTestPdfPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "TestAssets", "axf-annotation-1.pdf");
    }

    private sealed class RecordingWriteStream : Stream
    {
        internal long BytesWritten { get; private set; }

        internal int MaximumWriteSize { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => BytesWritten;
        public override long Position
        {
            get => BytesWritten;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            MaximumWriteSize = Math.Max(MaximumWriteSize, count);
            BytesWritten = checked(BytesWritten + count);
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
