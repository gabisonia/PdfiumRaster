using BenchmarkDotNet.Attributes;
using PdfiumRaster;

namespace PdfiumRaster.Benchmarks;

/// <summary>
/// Compares document reopening, session-owned output, caller-owned output, and scoped reusable output.
/// </summary>
[MemoryDiagnoser]
public class PdfRenderSessionBenchmarks
{
    private string _pdfPath = string.Empty;
    private PdfRenderSession _session = null!;
    private PdfImageConversionOptions _options = null!;
    private PdfImageConversionOptions _saveOptions = null!;
    private PdfBitmap _destination = null!;

    /// <summary>
    /// Gets or sets render DPI for the benchmark case.
    /// </summary>
    [Params(96, 144, 300)]
    public int Dpi { get; set; }

    /// <summary>
    /// Opens the reusable session and sizes the caller-owned destination.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdfPath = GetTestPdfPath("axf-annotation-1.pdf");
        _options = new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Dpi = Dpi },
        };
        _saveOptions = new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Dpi = Dpi },
            Format = PdfImageOutputFormat.Png,
            Encoding = PdfImageEncodingOptions.Fast,
        };
        _session = PdfRenderSession.Open(_pdfPath);
        var size = _session.RenderPage(0, bitmap => (bitmap.Width, bitmap.Height), _options);
        _destination = PdfBitmap.Create(size.Width, size.Height);
    }

    /// <summary>
    /// Disposes the reusable session.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _session.Dispose();
    }

    /// <summary>
    /// Measures the legacy convenience API that opens the PDF for every render.
    /// </summary>
    /// <returns>The rendered bitmap.</returns>
    [Benchmark(Baseline = true)]
    public PdfBitmap LegacyOpenAndRender()
    {
        return PdfImageConverter.RenderPage(_pdfPath, 0, _options);
    }

    /// <summary>
    /// Measures session rendering with an independently owned output bitmap.
    /// </summary>
    /// <returns>The rendered bitmap.</returns>
    [Benchmark]
    public PdfBitmap SessionOwnedBitmap()
    {
        return _session.RenderPage(0, _options);
    }

    /// <summary>
    /// Measures session rendering into a caller-owned bitmap.
    /// </summary>
    /// <returns>A pixel value that makes the render observable.</returns>
    [Benchmark]
    public byte SessionCallerOwnedBitmap()
    {
        _session.RenderPageInto(0, _destination, _options);
        return _destination.Pixels[0];
    }

    /// <summary>
    /// Measures scoped rendering into the session-owned reusable bitmap.
    /// </summary>
    /// <returns>A pixel value that makes the render observable.</returns>
    [Benchmark]
    public byte SessionScopedBitmap()
    {
        return _session.RenderPage(0, bitmap => bitmap.Pixels[0], _options);
    }

    /// <summary>
    /// Measures session rendering plus direct fast PNG encoding to a stream.
    /// </summary>
    /// <returns>The encoded byte count.</returns>
    [Benchmark]
    public long SessionSavePng()
    {
        using var stream = new MemoryStream();
        _session.SavePage(0, stream, _saveOptions);
        return stream.Length;
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
/// Isolates PNG encoding speed at the Skia default and explicit compression levels.
/// </summary>
[MemoryDiagnoser]
public class PngEncodingBenchmarks
{
    private PdfBitmap _bitmap = null!;
    private PdfImageEncodingOptions _encoding = null!;

    /// <summary>
    /// Gets or sets the PNG compression level; -1 selects the Skia default.
    /// </summary>
    [Params(-1, 1, 6, 9)]
    public int CompressionLevel { get; set; }

    /// <summary>
    /// Renders the source bitmap once so the benchmark measures only encoding.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        using var session = PdfRenderSession.Open(GetTestPdfPath("axf-annotation-1.pdf"));
        _bitmap = session.RenderPage(0, new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Dpi = 144 },
        });
        _encoding = new PdfImageEncodingOptions
        {
            PngCompressionLevel = CompressionLevel < 0 ? null : CompressionLevel,
        };
    }

    /// <summary>
    /// Encodes the pre-rendered bitmap into PNG.
    /// </summary>
    /// <returns>The encoded byte count.</returns>
    [Benchmark]
    public long EncodePng()
    {
        using var stream = new MemoryStream();
        PdfImageWriter.WritePng(_bitmap, stream, _encoding);
        return stream.Length;
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
