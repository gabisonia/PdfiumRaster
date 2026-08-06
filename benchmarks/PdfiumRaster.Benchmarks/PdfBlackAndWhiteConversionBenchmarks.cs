using BenchmarkDotNet.Attributes;
using PdfiumRaster;

namespace PdfiumRaster.Benchmarks;

/// <summary>
/// Measures the black-and-white threshold pass in isolation and inside a SaveDocument-shaped
/// render-threshold-encode round trip, so kernel changes can be judged by their end-to-end effect.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PdfBlackAndWhiteConversionBenchmarks
{
    private PdfiumLibrary _library = null!;
    private PdfDocument _document = null!;
    private PdfPage _page = null!;
    private PdfImageConversionOptions _options = null!;
    private PdfPageRenderOptions _renderOptions = null!;
    private PdfNativeBitmapLease _thresholdLease = null!;
    private int _width;
    private int _height;

    [Params(96, 300)]
    public int Dpi { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _library = PdfiumLibrary.Initialize();
        _document = PdfDocument.Load(GetTestPdfPath("axf-annotation-1.pdf"));
        _page = _document.LoadPage(0);
        _options = new PdfImageConversionOptions
        {
            Format = PdfImageOutputFormat.Png,
            ColorMode = PdfImageColorMode.BlackAndWhite,
            BlackAndWhiteThreshold = 128,
            Render = new PdfPageRenderOptions { Dpi = Dpi },
            Encoding = PdfImageEncodingOptions.Fast,
        };
        _renderOptions = PdfImageConverter.GetRenderOptions(_options);
        (_width, _height) = _renderOptions.GetPixelSize(_page.Width, _page.Height);
        _thresholdLease = PdfNativeBitmapLease.Create(_width, _height);
        PdfImageConverter.RenderToLease(_page, _thresholdLease, _renderOptions, _options);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _thresholdLease.Dispose();
        _page.Dispose();
        _document.Dispose();
        _library.Dispose();
    }

    [Benchmark]
    public void ThresholdPass()
    {
        PdfImageConverter.ApplyConversionColorMode(_thresholdLease, _options);
    }

    [Benchmark]
    public long RenderThresholdEncodePng()
    {
        using var bitmap = PdfNativeBitmapLease.Create(_width, _height);
        PdfImageConverter.RenderToLease(_page, bitmap, _renderOptions, _options);
        using var output = new CountingWriteStream();
        PdfImageConverter.SaveBitmap(bitmap, output, PdfImageOutputFormat.Png, _options.Encoding);
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
}
