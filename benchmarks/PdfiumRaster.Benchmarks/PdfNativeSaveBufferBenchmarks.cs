using BenchmarkDotNet.Attributes;
using PdfiumRaster;

namespace PdfiumRaster.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class PdfNativeSaveBufferBenchmarks
{
    private PdfiumLibrary _library = null!;
    private PdfDocument _document = null!;
    private PdfPage _page = null!;
    private PdfImageConversionOptions _options = null!;
    private PdfPageRenderOptions _renderOptions = null!;
    private int _width;
    private int _height;

    [Params(96, 144, 300)]
    public int Dpi { get; set; }

    [Params(
        PdfImageOutputFormat.Bmp,
        PdfImageOutputFormat.Png,
        PdfImageOutputFormat.Jpeg,
        PdfImageOutputFormat.Webp)]
    public PdfImageOutputFormat Format { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _library = PdfiumLibrary.Initialize();
        var configuredPdfPath = Environment.GetEnvironmentVariable("PDFIUMRASTER_BENCHMARK_PDF");
        var pdfPath = string.IsNullOrWhiteSpace(configuredPdfPath)
            ? GetTestPdfPath("axf-annotation-1.pdf")
            : ResolveConfiguredPdfPath(configuredPdfPath);
        _document = PdfDocument.Load(pdfPath);
        _page = _document.LoadPage(0);
        _options = new PdfImageConversionOptions
        {
            Format = Format,
            Render = new PdfPageRenderOptions { Dpi = Dpi },
            Encoding = PdfImageEncodingOptions.Fast,
        };
        _renderOptions = PdfImageConverter.GetRenderOptions(_options);
        (_width, _height) = _renderOptions.GetPixelSize(_page.Width, _page.Height);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _page.Dispose();
        _document.Dispose();
        _library.Dispose();
    }

    [Benchmark(Baseline = true)]
    public long ManagedSaveBuffer()
    {
        using var bitmap = PdfBitmapLease.Rent(_width, _height, clear: false);
        var rendered = PdfImageConverter.RenderToLease(_page, bitmap, _renderOptions, _options);
        using var output = new CountingWriteStream();
        PdfImageConverter.SaveBitmap(rendered, output, Format, _options.Encoding);
        return output.BytesWritten;
    }

    [Benchmark]
    public long NativeSaveBuffer()
    {
        using var bitmap = PdfNativeBitmapLease.Create(_width, _height);
        PdfImageConverter.RenderToLease(_page, bitmap, _renderOptions, _options);
        using var output = new CountingWriteStream();
        PdfImageConverter.SaveBitmap(bitmap, output, Format, _options.Encoding);
        return output.BytesWritten;
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

    private static string ResolveConfiguredPdfPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(directory.FullName, configuredPath);
            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find configured benchmark PDF '{configuredPath}'.");
    }
}
