using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PdfiumRaster;

BenchmarkSwitcher.FromAssembly(typeof(PdfRenderingBenchmarks).Assembly).Run(args);

[MemoryDiagnoser]
public class PdfRenderingBenchmarks
{
    private string _pdfPath = string.Empty;
    private string _outputDirectory = string.Empty;
    private byte[] _colorPixels = [];

    [GlobalSetup]
    public void Setup()
    {
        _pdfPath = GetTestPdfPath("axf-annotation-1.pdf");
        _outputDirectory = Path.Combine(Path.GetTempPath(), "PdfiumRaster.Benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);

        _colorPixels = CreateColorPixels(width: 1200, height: 1600);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Benchmark]
    public PdfBitmap RenderPageNumber()
    {
        return PdfImageConverter.RenderPageNumber(
            _pdfPath,
            pageNumber: 1,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 144 },
            });
    }

    [Benchmark]
    public byte[] SavePngToStream()
    {
        using var stream = new MemoryStream();

        PdfImageConverter.SavePng(
            _pdfPath,
            pageNumber: 1,
            stream,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 144 },
            });

        return stream.ToArray();
    }

    [Benchmark]
    public byte[] SaveJpegToStream()
    {
        using var stream = new MemoryStream();

        PdfImageConverter.SaveJpeg(
            _pdfPath,
            pageNumber: 1,
            stream,
            new PdfImageConversionOptions
            {
                Render = new PdfPageRenderOptions { Dpi = 144 },
            });

        return stream.ToArray();
    }

    [Benchmark]
    public int SaveDocumentAsBmp()
    {
        var outputDirectory = Path.Combine(_outputDirectory, Guid.NewGuid().ToString("N"));

        return PdfImageConverter.SaveDocument(
            _pdfPath,
            outputDirectory,
            fileNamePrefix: "page",
            options: new PdfImageConversionOptions
            {
                Format = PdfImageOutputFormat.Bmp,
                Render = new PdfPageRenderOptions { Dpi = 144 },
            });
    }

    [Benchmark]
    public PdfBitmap ApplyColorModeBlackAndWhite()
    {
        var pixels = new byte[_colorPixels.Length];
        Buffer.BlockCopy(_colorPixels, 0, pixels, 0, _colorPixels.Length);

        var bitmap = new PdfBitmap(width: 1200, height: 1600, stride: 1200 * 4, pixels);
        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.BlackAndWhite);

        return bitmap;
    }

    private static string GetTestPdfPath(string fileName)
    {
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (baseDirectory is not null)
        {
            var path = Path.Combine(baseDirectory.FullName, "tests", "PdfiumRaster.Tests", "TestAssets", fileName);
            if (File.Exists(path))
            {
                return path;
            }

            baseDirectory = baseDirectory.Parent;
        }

        throw new FileNotFoundException($"Could not find benchmark PDF asset '{fileName}'.");
    }

    private static byte[] CreateColorPixels(int width, int height)
    {
        var pixels = new byte[checked(width * height * 4)];

        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = (byte)(i % 251);
            pixels[i + 1] = (byte)((i / 3) % 251);
            pixels[i + 2] = (byte)((i / 7) % 251);
            pixels[i + 3] = 255;
        }

        return pixels;
    }
}