using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfBitmapLeaseTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void Rent_rejects_invalid_dimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfBitmapLease.Rent(width, height));
    }

    [Fact]
    public void Rent_exposes_bitmap_with_requested_dimensions()
    {
        using var lease = PdfBitmapLease.Rent(width: 3, height: 2);

        Assert.Equal(3, lease.Bitmap.Width);
        Assert.Equal(2, lease.Bitmap.Height);
        Assert.Equal(12, lease.Bitmap.Stride);
        Assert.True(lease.Bitmap.Pixels.Length >= 24);
    }

    [Fact]
    public void Dispose_is_idempotent_and_rejects_bitmap_access()
    {
        var lease = PdfBitmapLease.Rent(width: 1, height: 1);

        lease.Dispose();
        lease.Dispose();

        Assert.Throws<ObjectDisposedException>(() => lease.Bitmap);
    }

    [Fact]
    public void Leased_bitmap_can_be_used_as_render_destination()
    {
        using var pdfium = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath("TestAssets/smoke.pdf"));
        using var page = document.LoadPage(0);

        var options = new PdfPageRenderOptions { Dpi = 72 };
        var (width, height) = options.GetPixelSize(page.Width, page.Height);

        using var lease = PdfBitmapLease.Rent(width, height);
        page.Render(lease.Bitmap, options);

        Assert.Contains(lease.Bitmap.Pixels[..checked(lease.Bitmap.Stride * lease.Bitmap.Height)], pixel => pixel != 0);
    }

    [Fact]
    public void Leased_bitmap_can_reuse_native_bitmap_for_repeated_rendering()
    {
        using var pdfium = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath("TestAssets/smoke.pdf"));
        using var page = document.LoadPage(0);

        var options = new PdfPageRenderOptions { Dpi = 72 };
        var (width, height) = options.GetPixelSize(page.Width, page.Height);

        using var lease = PdfBitmapLease.Rent(width, height, clear: false);
        page.Render(lease, options);
        var firstRender = lease.Bitmap.Pixels[..checked(lease.Bitmap.Stride * lease.Bitmap.Height)].ToArray();

        Array.Clear(lease.Bitmap.Pixels, 0, checked(lease.Bitmap.Stride * lease.Bitmap.Height));
        page.Render(lease, options);

        Assert.Equal(firstRender, lease.Bitmap.Pixels[..firstRender.Length]);
    }

    [Fact]
    public void Render_rejects_disposed_bitmap_lease()
    {
        using var pdfium = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath("TestAssets/smoke.pdf"));
        using var page = document.LoadPage(0);
        var lease = PdfBitmapLease.Rent(width: 612, height: 792);
        lease.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
            page.Render(lease, new PdfPageRenderOptions { Dpi = 72 }));
    }

    private static string GetTestPdfPath(string relativePdfPath)
    {
        return Path.Combine(AppContext.BaseDirectory, relativePdfPath);
    }
}
