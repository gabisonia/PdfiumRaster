using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfAnnotationRenderingTests
{
    [Fact]
    public void Annotations_pdf_is_available_as_test_asset()
    {
        if (!TryGetAnnotationsPdfPath(out var pdfPath))
        {
            return;
        }

        Assert.True(File.Exists(pdfPath));
    }

    [Fact]
    public void Render_with_annotations_changes_pixels_for_annotations_pdf()
    {
        if (!IsPdfiumNativeAvailable())
        {
            return;
        }

        if (!TryGetAnnotationsPdfPath(out var pdfPath))
        {
            return;
        }

        using var pdfium = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath);
        using var page = document.LoadPage(0);

        var renderOptions = new PdfPageRenderOptions
        {
            Dpi = 96,
            Flags = PdfRenderFlags.LcdText,
        };

        var withoutAnnotations = page.Render(renderOptions);

        renderOptions.Flags = PdfRenderFlags.LcdText | PdfRenderFlags.Annot;
        var withAnnotations = page.Render(renderOptions);

        Assert.Equal(withoutAnnotations.Width, withAnnotations.Width);
        Assert.Equal(withoutAnnotations.Height, withAnnotations.Height);
        Assert.NotEqual(withoutAnnotations.Pixels, withAnnotations.Pixels);
    }

    [Fact]
    public void Export_all_annotations_pdf_pages_to_test_output()
    {
        Assert.True(
            IsPdfiumNativeAvailable(),
            "PDFium native library was not found. Add libpdfium.dylib/pdfium.dll/libpdfium.so beside the test output or in the platform loader path to generate images.");

        if (!TryGetAnnotationsPdfPath(out var pdfPath))
        {
            return;
        }

        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestOutput", "annotations");

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var pageCount = PdfImageConverter.SaveDocument(
            pdfPath,
            outputDirectory,
            fileNamePrefix: "annotations-page",
            options: new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions
                {
                    Dpi = 144,
                    Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
                },
                ColorMode = PdfImageColorMode.Color,
            });

        var images = Directory.GetFiles(outputDirectory, "*.bmp");

        Assert.Equal(pageCount, images.Length);
        Assert.All(images, image => Assert.True(new FileInfo(image).Length > 54));
    }

    [Fact]
    public void Can_query_page_count_and_sizes_from_bytes()
    {
        if (!TryGetAnnotationsPdfPath(out var pdfPath))
        {
            return;
        }

        var pdfBytes = File.ReadAllBytes(pdfPath);

        var pageCount = PdfImageConverter.GetPageCount(pdfBytes);
        var pageSizes = PdfImageConverter.GetPageSizes(pdfBytes);

        Assert.Equal(3, pageCount);
        Assert.Equal(pageCount, pageSizes.Count);
        Assert.All(pageSizes, size =>
        {
            Assert.True(size.Width > 0);
            Assert.True(size.Height > 0);
        });
    }

    [Fact]
    public void Can_query_page_count_from_seekable_stream_without_closing_when_leave_open()
    {
        if (!TryGetAnnotationsPdfPath(out var pdfPath))
        {
            return;
        }

        using var stream = File.OpenRead(pdfPath);

        var pageCount = PdfImageConverter.GetPageCount(stream, leaveOpen: true);

        Assert.Equal(3, pageCount);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public void Seekable_stream_stays_open_until_document_is_disposed()
    {
        if (!TryGetAnnotationsPdfPath(out var pdfPath))
        {
            return;
        }

        using var pdfium = PdfiumLibrary.Initialize();
        using var stream = File.OpenRead(pdfPath);

        using (var document = PdfDocument.Load(stream, leaveOpen: false))
        {
            Assert.Equal(3, document.PageCount);
            Assert.True(stream.CanRead);
        }

        Assert.False(stream.CanRead);
    }

    [Fact]
    public void Can_export_page_as_png_using_page_number()
    {
        if (!TryGetAnnotationsPdfPath(out var pdfPath))
        {
            return;
        }

        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestOutput", "formats");
        Directory.CreateDirectory(outputDirectory);

        var imagePath = Path.Combine(outputDirectory, "annotations-page-0001.png");

        PdfImageConverter.SavePng(
            pdfPath,
            pageNumber: 1,
            imagePath,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions
                {
                    Dpi = 96,
                    Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
                },
            });

        var bytes = File.ReadAllBytes(imagePath);

        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
    }

    private static bool TryGetAnnotationsPdfPath(out string path)
    {
        path = Path.Combine(AppContext.BaseDirectory, "TestAssets", "annotations.pdf");
        return File.Exists(path);
    }

    private static bool IsPdfiumNativeAvailable()
    {
        try
        {
            using var _ = PdfiumLibrary.Initialize();
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
    }
}