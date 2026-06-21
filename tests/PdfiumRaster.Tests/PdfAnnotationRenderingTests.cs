using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfAnnotationRenderingTests
{
    [Theory]
    [InlineData("TestAssets/smoke.pdf")]
    [InlineData("TestAssets/axf-annotation-1.pdf")]
    public void Pdf_is_available_as_test_asset(string relativePdfPath)
    {
        Assert.True(File.Exists(GetTestPdfPath(relativePdfPath)));
    }

    [Theory]
    [Trait("Category", "Local")]
    [InlineData("TestAssets/annotations.pdf")]
    public void Local_annotations_pdf_is_available_as_test_asset(string relativePdfPath)
    {
        Assert.True(File.Exists(GetTestPdfPath(relativePdfPath)));
    }

    [Theory]
    [Trait("Category", "Local")]
    [InlineData("TestAssets/annotations.pdf")]
    public void Export_all_local_annotations_pdf_pages_to_test_output(string relativePdfPath)
    {
        Assert.True(
            IsPdfiumNativeAvailable(),
            "PDFium native library was not found. Add libpdfium.dylib/pdfium.dll/libpdfium.so beside the test output or in the platform loader path to generate images.");

        var pdfPath = GetTestPdfPath(relativePdfPath);
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

    [Theory]
    [InlineData("TestAssets/axf-annotation-1.pdf")]
    public void Render_with_annotations_changes_pixels_for_annotation_pdf(string relativePdfPath)
    {
        if (!IsPdfiumNativeAvailable())
        {
            return;
        }

        var pdfPath = GetTestPdfPath(relativePdfPath);

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

    [Theory]
    [InlineData("TestAssets/smoke.pdf")]
    [InlineData("TestAssets/axf-annotation-1.pdf")]
    public void Export_all_pdf_pages_to_test_output(string relativePdfPath)
    {
        Assert.True(
            IsPdfiumNativeAvailable(),
            "PDFium native library was not found. Add libpdfium.dylib/pdfium.dll/libpdfium.so beside the test output or in the platform loader path to generate images.");

        var pdfPath = GetTestPdfPath(relativePdfPath);
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestOutput", Path.GetFileNameWithoutExtension(relativePdfPath));

        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var pageCount = PdfImageConverter.SaveDocument(
            pdfPath,
            outputDirectory,
            fileNamePrefix: "page",
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

    [Theory]
    [InlineData("TestAssets/smoke.pdf")]
    [InlineData("TestAssets/axf-annotation-1.pdf")]
    public void Can_query_page_count_and_sizes_from_bytes(string relativePdfPath)
    {
        var pdfBytes = File.ReadAllBytes(GetTestPdfPath(relativePdfPath));

        var pageCount = PdfImageConverter.GetPageCount(pdfBytes);
        var pageSizes = PdfImageConverter.GetPageSizes(pdfBytes);

        Assert.True(pageCount > 0);
        Assert.Equal(pageCount, pageSizes.Count);
        Assert.All(pageSizes, size =>
        {
            Assert.True(size.Width > 0);
            Assert.True(size.Height > 0);
        });
    }

    [Theory]
    [InlineData("TestAssets/smoke.pdf")]
    [InlineData("TestAssets/axf-annotation-1.pdf")]
    public void Can_query_page_count_from_seekable_stream_without_closing_when_leave_open(string relativePdfPath)
    {
        using var stream = File.OpenRead(GetTestPdfPath(relativePdfPath));

        var pageCount = PdfImageConverter.GetPageCount(stream, leaveOpen: true);

        Assert.True(pageCount > 0);
        Assert.True(stream.CanRead);
    }

    [Theory]
    [InlineData("TestAssets/smoke.pdf")]
    [InlineData("TestAssets/axf-annotation-1.pdf")]
    public void Seekable_stream_stays_open_until_document_is_disposed(string relativePdfPath)
    {
        using var pdfium = PdfiumLibrary.Initialize();
        using var stream = File.OpenRead(GetTestPdfPath(relativePdfPath));

        using (var document = PdfDocument.Load(stream, leaveOpen: false))
        {
            Assert.True(document.PageCount > 0);
            Assert.True(stream.CanRead);
        }

        Assert.False(stream.CanRead);
    }

    [Theory]
    [InlineData("TestAssets/smoke.pdf")]
    [InlineData("TestAssets/axf-annotation-1.pdf")]
    public void Can_export_page_as_png_using_page_number(string relativePdfPath)
    {
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestOutput", "formats");
        Directory.CreateDirectory(outputDirectory);

        var imagePath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(relativePdfPath)}-page-0001.png");

        PdfImageConverter.SavePng(
            GetTestPdfPath(relativePdfPath),
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

    private static string GetTestPdfPath(string relativePdfPath)
    {
        return Path.Combine(AppContext.BaseDirectory, relativePdfPath);
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
