using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfImageConverterTests
{
    [Fact]
    public void SavePng_writes_png_to_stream()
    {
        using var stream = new MemoryStream();

        PdfImageConverter.SavePng(
            GetTestPdfPath("TestAssets/smoke.pdf"),
            pageNumber: 1,
            stream,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 96 },
            });

        var bytes = stream.ToArray();

        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    [Fact]
    public void SaveJpeg_writes_jpeg_to_stream()
    {
        using var stream = new MemoryStream();

        PdfImageConverter.SaveJpeg(
            GetTestPdfPath("TestAssets/smoke.pdf"),
            pageNumber: 1,
            stream,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 96 },
            });

        var bytes = stream.ToArray();

        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xD8, bytes[1]);
    }

    [Fact]
    public void SaveWebp_writes_webp_to_stream()
    {
        using var stream = new MemoryStream();

        PdfImageConverter.SaveWebp(
            GetTestPdfPath("TestAssets/smoke.pdf"),
            pageNumber: 1,
            stream,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 96 },
            });

        var bytes = stream.ToArray();

        Assert.Equal((byte)'R', bytes[0]);
        Assert.Equal((byte)'I', bytes[1]);
        Assert.Equal((byte)'F', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
        Assert.Equal((byte)'W', bytes[8]);
        Assert.Equal((byte)'E', bytes[9]);
        Assert.Equal((byte)'B', bytes[10]);
        Assert.Equal((byte)'P', bytes[11]);
    }

    [Fact]
    public void SavePage_stream_rejects_null_stream()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PdfImageConverter.SavePage(GetTestPdfPath("TestAssets/smoke.pdf"), 0, (Stream)null!));
    }

    [Fact]
    public void SavePages_writes_only_selected_pages()
    {
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestOutput", "selected-pages");

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var savedCount = PdfImageConverter.SavePages(
            GetTestPdfPath("TestAssets/axf-annotation-1.pdf"),
            [1],
            outputDirectory,
            options: new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 96 },
            });

        var images = Directory.GetFiles(outputDirectory, "*.bmp");

        Assert.Equal(1, savedCount);
        Assert.Single(images);
        Assert.EndsWith("page-0002.bmp", images[0]);
    }

    [Fact]
    public void SavePageNumbers_writes_selected_one_based_page_numbers()
    {
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestOutput", "selected-page-numbers");

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var savedCount = PdfImageConverter.SavePageNumbers(
            GetTestPdfPath("TestAssets/axf-annotation-1.pdf"),
            [2],
            outputDirectory,
            options: new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 96 },
            });

        var images = Directory.GetFiles(outputDirectory, "*.bmp");

        Assert.Equal(1, savedCount);
        Assert.Single(images);
        Assert.EndsWith("page-0002.bmp", images[0]);
    }

    [Fact]
    public void RenderPageNumber_rejects_zero_page_number()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PdfImageConverter.RenderPageNumber("sample.pdf", 0));
    }

    [Fact]
    public void ApplyColorMode_grayscale_sets_rgb_channels_to_luminance()
    {
        var bitmap = new PdfBitmap(
            width: 1,
            height: 1,
            stride: 4,
            pixels: [10, 20, 30, 255]);

        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.Grayscale);

        Assert.Equal(22, bitmap.Pixels[0]);
        Assert.Equal(22, bitmap.Pixels[1]);
        Assert.Equal(22, bitmap.Pixels[2]);
        Assert.Equal(255, bitmap.Pixels[3]);
    }

    [Fact]
    public void ApplyColorMode_black_and_white_thresholds_luminance()
    {
        var bitmap = new PdfBitmap(
            width: 2,
            height: 1,
            stride: 8,
            pixels:
            [
                10, 10, 10, 255,
                240, 240, 240, 255,
            ]);

        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.BlackAndWhite, blackAndWhiteThreshold: 128);

        Assert.Equal([0, 0, 0, 255, 255, 255, 255, 255], bitmap.Pixels);
    }

    [Fact]
    public void Conversion_black_and_white_thresholds_grayscale_rendered_pixels()
    {
        var bitmap = PdfImageConverter.RenderPageNumber(
            GetTestPdfPath("TestAssets/smoke.pdf"),
            pageNumber: 1,
            new PdfImageConversionOptions
            {
                ColorMode = PdfImageColorMode.BlackAndWhite,
                BlackAndWhiteThreshold = 128,
                Render = new PdfPageRenderOptions { Dpi = 72 },
            });

        for (var i = 0; i < bitmap.Pixels.Length; i += 4)
        {
            Assert.True(bitmap.Pixels[i] is 0 or 255);
            Assert.Equal(bitmap.Pixels[i], bitmap.Pixels[i + 1]);
            Assert.Equal(bitmap.Pixels[i], bitmap.Pixels[i + 2]);
        }
    }

    [Fact]
    public void Conversion_grayscale_renders_equal_rgb_channels()
    {
        var bitmap = PdfImageConverter.RenderPageNumber(
            GetTestPdfPath("TestAssets/smoke.pdf"),
            pageNumber: 1,
            new PdfImageConversionOptions
            {
                ColorMode = PdfImageColorMode.Grayscale,
                Render = new PdfPageRenderOptions { Dpi = 72 },
            });

        for (var i = 0; i < bitmap.Pixels.Length; i += 4)
        {
            Assert.Equal(bitmap.Pixels[i], bitmap.Pixels[i + 1]);
            Assert.Equal(bitmap.Pixels[i], bitmap.Pixels[i + 2]);
        }
    }

    [Fact]
    public void RenderPageInto_renders_into_existing_bitmap()
    {
        var pdfPath = GetTestPdfPath("TestAssets/smoke.pdf");
        var options = new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Dpi = 72 },
        };
        var pageSize = PdfImageConverter.GetPageSizes(pdfPath)[0];
        var (width, height) = options.Render.GetPixelSize(pageSize.Width, pageSize.Height);
        var bitmap = PdfBitmap.Create(width, height);

        PdfImageConverter.RenderPageInto(pdfPath, pageIndex: 0, bitmap, options);

        Assert.Contains(bitmap.Pixels, pixel => pixel != 0);
    }

    [Fact]
    public void RenderPage_open_document_avoids_reopening_document()
    {
        using var pdfium = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath("TestAssets/smoke.pdf"));

        var bitmap = PdfImageConverter.RenderPage(
            document,
            pageIndex: 0,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 72 },
            });

        Assert.Contains(bitmap.Pixels, pixel => pixel != 0);
    }

    [Fact]
    public void RenderPageInto_open_document_reuses_destination()
    {
        using var pdfium = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath("TestAssets/smoke.pdf"));
        using var page = document.LoadPage(0);

        var options = new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Dpi = 72 },
        };
        var (width, height) = options.Render.GetPixelSize(page.Width, page.Height);
        var bitmap = PdfBitmap.Create(width, height);

        PdfImageConverter.RenderPageInto(document, pageIndex: 0, bitmap, options);

        Assert.Contains(bitmap.Pixels, pixel => pixel != 0);
    }

    [Fact]
    public void RenderPage_open_document_rejects_null_document()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PdfImageConverter.RenderPage(
                (PdfDocument)null!,
                pageIndex: 0,
                new PdfImageConversionOptions()));
    }

    [Fact]
    public void RenderPageNumberInto_uses_one_based_page_number()
    {
        var pdfPath = GetTestPdfPath("TestAssets/axf-annotation-1.pdf");
        var options = new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Dpi = 72 },
        };
        var pageSize = PdfImageConverter.GetPageSizes(pdfPath)[1];
        var (width, height) = options.Render.GetPixelSize(pageSize.Width, pageSize.Height);
        var bitmap = PdfBitmap.Create(width, height);

        PdfImageConverter.RenderPageNumberInto(pdfPath, pageNumber: 2, bitmap, options);

        Assert.Contains(bitmap.Pixels, pixel => pixel != 0);
    }

    [Fact]
    public void RenderPageInto_rejects_null_destination()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PdfImageConverter.RenderPageInto(GetTestPdfPath("TestAssets/smoke.pdf"), 0, null!));
    }

    [Fact]
    public void RenderPageInto_rejects_mismatched_destination_size()
    {
        var bitmap = PdfBitmap.Create(width: 1, height: 1);

        Assert.Throws<ArgumentException>(() =>
            PdfImageConverter.RenderPageInto(GetTestPdfPath("TestAssets/smoke.pdf"), 0, bitmap,
                new PdfImageConversionOptions
                {
                    Render = new PdfPageRenderOptions { Dpi = 72 },
                }));
    }

    private static string GetTestPdfPath(string relativePdfPath)
    {
        return Path.Combine(AppContext.BaseDirectory, relativePdfPath);
    }
}
