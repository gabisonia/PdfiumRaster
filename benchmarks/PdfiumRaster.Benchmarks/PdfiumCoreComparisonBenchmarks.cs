using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using PDFiumCore;
using PdfiumRaster;

[MemoryDiagnoser]
public class PdfiumCoreComparisonBenchmarks
{
    private const int BgraBitmapFormat = 4;
    private const int RenderAnnotationsAndLcdText = 0x01 | 0x02;
    private const uint WhiteBackground = 0xFFFFFFFF;

    private PdfiumLibrary _library = null!;
    private PdfPageRenderOptions _renderOptions = null!;
    private PdfDocument _rasterDocument = null!;
    private PdfPage _rasterPage = null!;
    private PdfBitmapLease _rasterBitmap = null!;

    private FpdfDocumentT _coreDocument = null!;
    private FpdfPageT _corePage = null!;
    private FpdfBitmapT _coreBitmap = null!;
    private byte[] _corePixels = [];
    private GCHandle _pinnedCorePixels;

    private int _width;
    private int _height;
    private int _stride;

    [GlobalSetup]
    public void Setup()
    {
        var pdfPath = GetTestPdfPath("axf-annotation-1.pdf");
        _renderOptions = new PdfPageRenderOptions
        {
            Dpi = 144,
            Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
            BackgroundColor = WhiteBackground,
        };

        // PdfiumRaster owns the single process-wide PDFium initialization. PDFiumCore then calls the same loaded
        // native binary, keeping the engine version and initialization state identical for both benchmark methods.
        _library = PdfiumLibrary.Initialize();

        _rasterDocument = PdfDocument.Load(pdfPath);
        _rasterPage = _rasterDocument.LoadPage(0);
        (_width, _height) = _renderOptions.GetPixelSize(_rasterPage.Width, _rasterPage.Height);
        _stride = checked(_width * 4);
        _rasterBitmap = PdfBitmapLease.Rent(_width, _height, clear: false);

        _coreDocument = fpdfview.FPDF_LoadDocument(pdfPath, null)
                        ?? throw new InvalidOperationException("PDFiumCore could not load the benchmark PDF.");
        _corePage = fpdfview.FPDF_LoadPage(_coreDocument, 0)
                    ?? throw new InvalidOperationException("PDFiumCore could not load the benchmark page.");

        _corePixels = new byte[checked(_stride * _height)];
        _pinnedCorePixels = GCHandle.Alloc(_corePixels, GCHandleType.Pinned);
        _coreBitmap = fpdfview.FPDFBitmapCreateEx(
                          _width,
                          _height,
                          BgraBitmapFormat,
                          _pinnedCorePixels.AddrOfPinnedObject(),
                          _stride)
                      ?? throw new InvalidOperationException("PDFiumCore could not create the benchmark bitmap.");

        // Create PdfiumRaster's persistent native bitmap before measurements, matching PDFiumCore setup.
        _rasterPage.Render(_rasterBitmap, _renderOptions);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_coreBitmap is not null)
        {
            fpdfview.FPDFBitmapDestroy(_coreBitmap);
        }

        if (_pinnedCorePixels.IsAllocated)
        {
            _pinnedCorePixels.Free();
        }

        if (_corePage is not null)
        {
            fpdfview.FPDF_ClosePage(_corePage);
        }

        if (_coreDocument is not null)
        {
            fpdfview.FPDF_CloseDocument(_coreDocument);
        }

        _rasterBitmap.Dispose();
        _rasterPage.Dispose();
        _rasterDocument.Dispose();
        _library.Dispose();
    }

    [Benchmark(Baseline = true)]
    public byte PdfiumRaster_OpenPage_ReusedBitmap()
    {
        _rasterPage.Render(_rasterBitmap, _renderOptions);

        return _rasterBitmap.Bitmap.Pixels[0];
    }

    [Benchmark]
    public byte PdfiumCore_OpenPage_ReusedBitmap()
    {
        fpdfview.FPDFBitmapFillRect(_coreBitmap, 0, 0, _width, _height, WhiteBackground);
        fpdfview.FPDF_RenderPageBitmap(
            _coreBitmap,
            _corePage,
            0,
            0,
            _width,
            _height,
            0,
            RenderAnnotationsAndLcdText);

        return _corePixels[0];
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
}
