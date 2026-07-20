using BenchmarkDotNet.Attributes;
using PdfiumRaster;

namespace PdfiumRaster.Benchmarks;

/// <summary>
/// Compares sequential and two-buffer pipelined multi-page image exports without file-system noise.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PdfDocumentPipelineBenchmarks
{
    private static readonly int[] PageIndexes = [0, 1, 0, 1];

    private string _pdfPath = string.Empty;
    private PdfImageConversionOptions _options = null!;
    private long _encodedBytes;

    /// <summary>
    /// Gets or sets the render DPI.
    /// </summary>
    [Params(96, 144)]
    public int Dpi { get; set; }

    /// <summary>
    /// Gets or sets the compressed output scenario.
    /// </summary>
    [Params(
        BatchEncodingScenario.FastPng,
        BatchEncodingScenario.DefaultPng,
        BatchEncodingScenario.Jpeg)]
    public BatchEncodingScenario Scenario { get; set; }

    /// <summary>
    /// Initializes the PDF path and conversion settings.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdfPath = GetTestPdfPath("axf-annotation-1.pdf");
        _options = new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Dpi = Dpi },
            Format = Scenario == BatchEncodingScenario.Jpeg
                ? PdfImageOutputFormat.Jpeg
                : PdfImageOutputFormat.Png,
            Encoding = Scenario == BatchEncodingScenario.FastPng
                ? PdfImageEncodingOptions.Fast
                : new PdfImageEncodingOptions(),
        };
    }

    /// <summary>
    /// Renders and encodes four pages sequentially.
    /// </summary>
    /// <returns>Total encoded bytes.</returns>
    [Benchmark(Baseline = true)]
    public long Sequential()
    {
        return Run(usePipelinedEncoding: false);
    }

    /// <summary>
    /// Renders serially and encodes through two reusable bitmap slots.
    /// </summary>
    /// <returns>Total encoded bytes.</returns>
    [Benchmark]
    public long TwoBufferPipeline()
    {
        return Run(usePipelinedEncoding: true);
    }

    private long Run(bool usePipelinedEncoding)
    {
        Interlocked.Exchange(ref _encodedBytes, 0);

        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(_pdfPath);
        PdfImageConverter.SavePagesCore(
            document,
            PageIndexes,
            PageIndexes.Length,
            outputDirectory: "benchmark-output",
            fileNamePrefix: "page",
            _options,
            usePipelinedEncoding,
            Encode);

        return Interlocked.Read(ref _encodedBytes);
    }

    private void Encode(
        PdfNativeBitmapLease bitmap,
        string _,
        PdfImageOutputFormat format,
        PdfImageEncodingOptions encodingOptions)
    {
        using var output = new CountingWriteStream();
        PdfImageConverter.SaveBitmap(bitmap, output, format, encodingOptions);
        Interlocked.Add(ref _encodedBytes, output.BytesWritten);
    }

    private static string GetTestPdfPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, "tests", "PdfiumRaster.Tests", "TestAssets", fileName);
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find benchmark PDF asset '{fileName}'.");
    }
}

/// <summary>
/// Compressed-output scenarios used by multi-page pipeline benchmarks.
/// </summary>
public enum BatchEncodingScenario
{
    /// <summary>
    /// PNG compression level 1.
    /// </summary>
    FastPng,

    /// <summary>
    /// Skia's default PNG compression.
    /// </summary>
    DefaultPng,

    /// <summary>
    /// JPEG quality 100.
    /// </summary>
    Jpeg,
}
