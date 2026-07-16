using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfBatchSavePipelineTests
{
    [Fact]
    public void SavePagesCore_pipelines_two_encoders_and_reuses_two_buffers()
    {
        using var pdfium = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath("TestAssets/axf-annotation-1.pdf"));
        using var encodersStarted = new CountdownEvent(PdfBatchSavePipeline.BufferCount);
        var syncRoot = new object();
        var pixelBuffers = new HashSet<byte[]>();
        var activeEncoders = 0;
        var maximumActiveEncoders = 0;
        var startedEncoders = 0;
        var encodedPages = 0;

        var savedCount = PdfImageConverter.SavePagesCore(
            document,
            pageIndexes: [0, 1, 0, 1],
            pageCount: 4,
            outputDirectory: "ignored",
            fileNamePrefix: "page",
            options: CreatePngOptions(),
            usePipelinedEncoding: true,
            encoder: (bitmap, _, _, _) =>
            {
                lock (syncRoot)
                {
                    activeEncoders++;
                    maximumActiveEncoders = Math.Max(maximumActiveEncoders, activeEncoders);
                    pixelBuffers.Add(bitmap.Pixels);
                }

                try
                {
                    if (Interlocked.Increment(ref startedEncoders) <= PdfBatchSavePipeline.BufferCount)
                    {
                        encodersStarted.Signal();
                    }

                    Assert.True(encodersStarted.Wait(TimeSpan.FromSeconds(10)),
                        "Both encoding workers did not start concurrently.");
                    Interlocked.Increment(ref encodedPages);
                }
                finally
                {
                    lock (syncRoot)
                    {
                        activeEncoders--;
                    }
                }
            });

        Assert.Equal(4, savedCount);
        Assert.Equal(4, encodedPages);
        Assert.Equal(PdfBatchSavePipeline.BufferCount, maximumActiveEncoders);
        Assert.Equal(PdfBatchSavePipeline.BufferCount, pixelBuffers.Count);
    }

    [Fact]
    public void SavePagesCore_propagates_encoder_failure()
    {
        using var pdfium = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(GetTestPdfPath("TestAssets/axf-annotation-1.pdf"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PdfImageConverter.SavePagesCore(
                document,
                pageIndexes: [0, 1],
                pageCount: 2,
                outputDirectory: "ignored",
                fileNamePrefix: "page",
                options: CreatePngOptions(),
                usePipelinedEncoding: true,
                encoder: (_, _, _, _) => throw new InvalidOperationException("Encoding failed.")));

        Assert.Equal("Encoding failed.", exception.Message);
    }

    [Fact]
    public void SaveDocument_writes_every_page_as_png_through_public_api()
    {
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestOutput", "pipelined-png");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var savedCount = PdfImageConverter.SaveDocument(
            GetTestPdfPath("TestAssets/axf-annotation-1.pdf"),
            outputDirectory,
            options: CreatePngOptions());

        var imagePaths = Directory.GetFiles(outputDirectory, "*.png").OrderBy(path => path).ToArray();
        Assert.Equal(savedCount, imagePaths.Length);
        Assert.True(savedCount >= 2);

        for (var index = 0; index < imagePaths.Length; index++)
        {
            var signature = File.ReadAllBytes(imagePaths[index]).Take(4).ToArray();
            Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], signature);
            Assert.EndsWith($"page-{index + 1:D4}.png", imagePaths[index]);
        }
    }

    [Fact]
    public void SavePages_pipelined_output_keeps_sorted_distinct_names()
    {
        var outputDirectory = Path.Combine(AppContext.BaseDirectory, "TestOutput", "pipelined-selected-png");
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        var savedCount = PdfImageConverter.SavePages(
            GetTestPdfPath("TestAssets/axf-annotation-1.pdf"),
            pageIndexes: [1, 0, 1],
            outputDirectory,
            options: CreatePngOptions());

        var fileNames = Directory.GetFiles(outputDirectory, "*.png")
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(fileName => fileName)
            .ToArray();

        Assert.Equal(2, savedCount);
        Assert.Equal(["page-0001.png", "page-0002.png"], fileNames);
    }

    private static PdfImageConversionOptions CreatePngOptions()
    {
        return new PdfImageConversionOptions
        {
            Format = PdfImageOutputFormat.Png,
            Render = PdfPageRenderOptions.ScreenPreview,
            Encoding = PdfImageEncodingOptions.Fast,
        };
    }

    private static string GetTestPdfPath(string relativePdfPath)
    {
        return Path.Combine(AppContext.BaseDirectory, relativePdfPath);
    }
}
