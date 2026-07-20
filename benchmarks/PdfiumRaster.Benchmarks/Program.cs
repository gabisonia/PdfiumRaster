using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PdfiumRaster;
using PdfiumRaster.Benchmarks;

if (args.Length > 0 && args[0] == "--all-pages-measure")
{
    PdfAllPagesMeasurement.Run(args[1..]);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(PdfRenderingBenchmarks).Assembly).Run(args);

[MemoryDiagnoser]
public class PdfRenderingBenchmarks
{
    private string _pdfPath = string.Empty;
    private string _outputDirectory = string.Empty;
    private byte[] _colorPixels = [];
    private PdfBitmap _renderIntoBitmap = null!;
    private PdfBitmapLease _renderLease = null!;
    private PdfBitmap _colorModeBitmap = null!;
    private PdfImageConversionOptions _dpi144Options = null!;
    private PdfiumLibrary _pdfium = null!;
    private PdfDocument _document = null!;
    private PdfPage _page = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pdfPath = GetTestPdfPath("axf-annotation-1.pdf");
        _outputDirectory = Path.Combine(Path.GetTempPath(), "PdfiumRaster.Benchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDirectory);

        _colorPixels = CreateColorPixels(width: 1200, height: 1600);
        _colorModeBitmap = new PdfBitmap(
            width: 1200,
            height: 1600,
            stride: 1200 * 4,
            pixels: new byte[_colorPixels.Length]);

        _dpi144Options = new PdfImageConversionOptions
        {
            Render = new PdfPageRenderOptions { Dpi = 144 },
        };

        _pdfium = PdfiumLibrary.Initialize();
        _document = PdfDocument.Load(_pdfPath);
        _page = _document.LoadPage(0);
        var (width, height) = _dpi144Options.Render.GetPixelSize(_page.Width, _page.Height);
        _renderIntoBitmap = PdfBitmap.Create(width, height);
        _renderLease = PdfBitmapLease.Rent(width, height, clear: false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _renderLease.Dispose();
        _page.Dispose();
        _document.Dispose();
        _pdfium.Dispose();

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
    public int RenderPageIntoExistingBitmap()
    {
        PdfImageConverter.RenderPageInto(_pdfPath, pageIndex: 0, _renderIntoBitmap, _dpi144Options);

        return _renderIntoBitmap.Width * _renderIntoBitmap.Height;
    }

    [Benchmark]
    public int RenderPageIntoOpenDocument()
    {
        PdfImageConverter.RenderPageInto(_document, pageIndex: 0, _renderIntoBitmap, _dpi144Options);

        return _renderIntoBitmap.Width * _renderIntoBitmap.Height;
    }

    [Benchmark]
    public byte RenderOpenPageIntoLeasedBitmap()
    {
        _page.Render(_renderLease, _dpi144Options.Render);

        return _renderLease.Bitmap.Pixels[0];
    }

    [Benchmark]
    public byte RenderOpenDocumentIntoLeasedBitmap()
    {
        using var page = _document.LoadPage(0);
        page.Render(_renderLease, _dpi144Options.Render);

        return _renderLease.Bitmap.Pixels[0];
    }

    [Benchmark]
    public int RepeatedSinglePageRender()
    {
        var totalPixels = 0;

        for (var i = 0; i < 3; i++)
        {
            var bitmap = PdfImageConverter.RenderPageNumber(
                _pdfPath,
                pageNumber: 1,
                new PdfImageConversionOptions
                {
                    Render = new PdfPageRenderOptions { Dpi = 144 },
                });

            totalPixels += bitmap.Width * bitmap.Height;
        }

        return totalPixels;
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
    public long SavePngToFile()
    {
        var outputPath = Path.Combine(_outputDirectory, "single-page.png");

        PdfImageConverter.SavePng(
            _pdfPath,
            pageNumber: 1,
            outputPath,
            _dpi144Options);

        return new FileInfo(outputPath).Length;
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
        var outputDirectory = Path.Combine(_outputDirectory, "bmp-document");

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
    public int SaveDocumentAsPng()
    {
        var outputDirectory = Path.Combine(_outputDirectory, "png-document");

        return PdfImageConverter.SaveDocument(
            _pdfPath,
            outputDirectory,
            fileNamePrefix: "page",
            options: new PdfImageConversionOptions
            {
                Format = PdfImageOutputFormat.Png,
                Render = new PdfPageRenderOptions { Dpi = 144 },
            });
    }

    [Benchmark]
    public int SaveDocumentAsJpeg()
    {
        var outputDirectory = Path.Combine(_outputDirectory, "jpeg-document");

        return PdfImageConverter.SaveDocument(
            _pdfPath,
            outputDirectory,
            fileNamePrefix: "page",
            options: new PdfImageConversionOptions
            {
                Format = PdfImageOutputFormat.Jpeg,
                Render = new PdfPageRenderOptions { Dpi = 144 },
            });
    }

    [Benchmark]
    public int SaveSelectedPagesAsPng()
    {
        var outputDirectory = Path.Combine(_outputDirectory, "selected-png-pages");

        return PdfImageConverter.SavePages(
            _pdfPath,
            [0, 1],
            outputDirectory,
            fileNamePrefix: "page",
            options: new PdfImageConversionOptions
            {
                Format = PdfImageOutputFormat.Png,
                Render = new PdfPageRenderOptions { Dpi = 144 },
            });
    }

    [Benchmark]
    public PdfBitmap ApplyColorModeColor()
    {
        var pixels = new byte[_colorPixels.Length];
        Buffer.BlockCopy(_colorPixels, 0, pixels, 0, _colorPixels.Length);

        var bitmap = new PdfBitmap(width: 1200, height: 1600, stride: 1200 * 4, pixels);
        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.Color);

        return bitmap;
    }

    [Benchmark]
    public PdfBitmap ApplyColorModeGrayscale()
    {
        var pixels = new byte[_colorPixels.Length];
        Buffer.BlockCopy(_colorPixels, 0, pixels, 0, _colorPixels.Length);

        var bitmap = new PdfBitmap(width: 1200, height: 1600, stride: 1200 * 4, pixels);
        PdfImageConverter.ApplyColorMode(bitmap, PdfImageColorMode.Grayscale);

        return bitmap;
    }

    [Benchmark]
    public byte ApplyColorModeGrayscaleInPlace()
    {
        Buffer.BlockCopy(_colorPixels, 0, _colorModeBitmap.Pixels, 0, _colorPixels.Length);
        PdfImageConverter.ApplyColorMode(_colorModeBitmap, PdfImageColorMode.Grayscale);

        return _colorModeBitmap.Pixels[0];
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

    [Benchmark]
    public byte ApplyColorModeBlackAndWhiteInPlace()
    {
        Buffer.BlockCopy(_colorPixels, 0, _colorModeBitmap.Pixels, 0, _colorPixels.Length);
        PdfImageConverter.ApplyColorMode(_colorModeBitmap, PdfImageColorMode.BlackAndWhite);

        return _colorModeBitmap.Pixels[0];
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
