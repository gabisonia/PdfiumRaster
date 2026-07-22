using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using PDFiumCore;
using PdfiumRaster;
using SkiaSharp;

internal static class PdfiumCoreComparisonSettings
{
    internal const int Dpi = 144;
    internal const int BgraBitmapFormat = 4;
    internal const int RenderAnnotationsAndLcdText = 0x01 | 0x02;
    internal const uint WhiteBackground = 0xFFFFFFFF;

    internal static PdfPageRenderOptions CreateRenderOptions()
    {
        return new PdfPageRenderOptions
        {
            Dpi = Dpi,
            Flags = PdfRenderFlags.Annot | PdfRenderFlags.LcdText,
            BackgroundColor = WhiteBackground,
        };
    }

    internal static string GetTestPdfPath()
    {
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (baseDirectory is not null)
        {
            var path = Path.Combine(
                baseDirectory.FullName,
                "tests",
                "PdfiumRaster.Tests",
                "TestAssets",
                "axf-annotation-1.pdf");
            if (File.Exists(path))
            {
                return path;
            }

            baseDirectory = baseDirectory.Parent;
        }

        throw new FileNotFoundException("Could not find benchmark PDF asset 'axf-annotation-1.pdf'.");
    }
}

/// <summary>
/// Compares wrapper overhead after the PDF document, page, and reusable destination bitmap are already open.
/// </summary>
[MemoryDiagnoser]
public class PdfiumCoreHotRenderComparisonBenchmarks
{
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
        var pdfPath = PdfiumCoreComparisonSettings.GetTestPdfPath();
        _renderOptions = PdfiumCoreComparisonSettings.CreateRenderOptions();

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
                          PdfiumCoreComparisonSettings.BgraBitmapFormat,
                          _pinnedCorePixels.AddrOfPinnedObject(),
                          _stride)
                      ?? throw new InvalidOperationException("PDFiumCore could not create the benchmark bitmap.");

        if (fpdfview.FPDFBitmapGetWidth(_coreBitmap) != _width ||
            fpdfview.FPDFBitmapGetHeight(_coreBitmap) != _height ||
            fpdfview.FPDFBitmapGetStride(_coreBitmap) != _stride)
        {
            throw new InvalidOperationException("PDFiumCore created a bitmap with unexpected dimensions or stride.");
        }

        // Create PdfiumRaster's persistent native bitmap before measurements, matching PDFiumCore setup, then render
        // both paths once and enforce exact raw-pixel equivalence outside the measured operations.
        _rasterPage.Render(_rasterBitmap, _renderOptions);
        RenderWithPdfiumCore();
        if (!_rasterBitmap.Bitmap.Pixels.AsSpan(0, checked(_stride * _height)).SequenceEqual(_corePixels))
        {
            throw new InvalidOperationException("Equivalent PdfiumRaster and PDFiumCore renders produced different pixels.");
        }
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
        RenderWithPdfiumCore();
        return _corePixels[0];
    }

    private void RenderWithPdfiumCore()
    {
        fpdfview.FPDFBitmapFillRect(
            _coreBitmap,
            0,
            0,
            _width,
            _height,
            PdfiumCoreComparisonSettings.WhiteBackground);
        fpdfview.FPDF_RenderPageBitmap(
            _coreBitmap,
            _corePage,
            0,
            0,
            _width,
            _height,
            0,
            PdfiumCoreComparisonSettings.RenderAnnotationsAndLcdText);
    }
}

/// <summary>
/// Compares a complete one-page fast-PNG workflow. PDFiumCore supplies the raw binding path; SkiaSharp supplies the
/// equivalent encoder that PdfiumRaster includes in its workflow.
/// </summary>
[MemoryDiagnoser]
public class PdfiumCorePngWorkflowComparisonBenchmarks
{
    private PdfiumLibrary _library = null!;
    private PdfImageConversionOptions _options = null!;
    private string _pdfPath = null!;
    private ResettableCountingWriteStream _rasterOutput = null!;
    private ResettableCountingWriteStream _coreOutput = null!;

    [GlobalSetup]
    public void Setup()
    {
        _pdfPath = PdfiumCoreComparisonSettings.GetTestPdfPath();
        _options = new PdfImageConversionOptions
        {
            Format = PdfImageOutputFormat.Png,
            Render = PdfiumCoreComparisonSettings.CreateRenderOptions(),
            Encoding = PdfImageEncodingOptions.Fast,
        };
        _rasterOutput = new ResettableCountingWriteStream();
        _coreOutput = new ResettableCountingWriteStream();

        // Keep PDFium initialized for the benchmark process so neither timed method includes one-time native startup.
        _library = PdfiumLibrary.Initialize();

        using var rasterPng = new MemoryStream();
        using var corePng = new MemoryStream();
        PdfImageConverter.SavePng(_pdfPath, pageNumber: 1, rasterPng, _options);
        SavePngWithPdfiumCore(_pdfPath, corePng);
        VerifyPng(rasterPng.ToArray(), corePng.ToArray());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _rasterOutput.Dispose();
        _coreOutput.Dispose();
        _library.Dispose();
    }

    [Benchmark(Baseline = true)]
    public long PdfiumRaster_FileToFastPng()
    {
        _rasterOutput.Reset();
        PdfImageConverter.SavePng(_pdfPath, pageNumber: 1, _rasterOutput, _options);
        return _rasterOutput.BytesWritten;
    }

    [Benchmark]
    public long PdfiumCoreAndSkia_FileToFastPng()
    {
        _coreOutput.Reset();
        SavePngWithPdfiumCore(_pdfPath, _coreOutput);
        return _coreOutput.BytesWritten;
    }

    private static void SavePngWithPdfiumCore(string pdfPath, Stream output)
    {
        var document = fpdfview.FPDF_LoadDocument(pdfPath, null)
                       ?? throw new InvalidOperationException("PDFiumCore could not load the benchmark PDF.");
        FpdfPageT? page = null;
        FpdfBitmapT? bitmap = null;

        try
        {
            page = fpdfview.FPDF_LoadPage(document, 0)
                   ?? throw new InvalidOperationException("PDFiumCore could not load the benchmark page.");

            var width = ToPixels(fpdfview.FPDF_GetPageWidthF(page));
            var height = ToPixels(fpdfview.FPDF_GetPageHeightF(page));
            bitmap = fpdfview.FPDFBitmapCreateEx(
                         width,
                         height,
                         PdfiumCoreComparisonSettings.BgraBitmapFormat,
                         IntPtr.Zero,
                         0)
                     ?? throw new InvalidOperationException("PDFiumCore could not create the benchmark bitmap.");

            fpdfview.FPDFBitmapFillRect(
                bitmap,
                0,
                0,
                width,
                height,
                PdfiumCoreComparisonSettings.WhiteBackground);
            fpdfview.FPDF_RenderPageBitmap(
                bitmap,
                page,
                0,
                0,
                width,
                height,
                0,
                PdfiumCoreComparisonSettings.RenderAnnotationsAndLcdText);

            var pixels = fpdfview.FPDFBitmapGetBuffer(bitmap);
            var stride = fpdfview.FPDFBitmapGetStride(bitmap);
            if (pixels == IntPtr.Zero || stride < checked(width * 4))
            {
                throw new InvalidOperationException("PDFiumCore returned an invalid bitmap buffer.");
            }

            var imageInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var pixmap = new SKPixmap(imageInfo, pixels, stride);
            var pngOptions = new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, zLibLevel: 1);
            if (!pixmap.Encode(output, pngOptions))
            {
                throw new InvalidOperationException("SkiaSharp could not encode the PDFiumCore bitmap.");
            }
        }
        finally
        {
            if (bitmap is not null)
            {
                fpdfview.FPDFBitmapDestroy(bitmap);
            }

            if (page is not null)
            {
                fpdfview.FPDF_ClosePage(page);
            }

            fpdfview.FPDF_CloseDocument(document);
        }
    }

    private static int ToPixels(float points)
    {
        return Math.Max(1, checked((int)Math.Ceiling(points / 72d * PdfiumCoreComparisonSettings.Dpi)));
    }

    private static void VerifyPng(byte[] rasterPng, byte[] corePng)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!rasterPng.AsSpan().StartsWith(signature) || !corePng.AsSpan().StartsWith(signature))
        {
            throw new InvalidOperationException("A comparison workflow did not produce a PNG image.");
        }

        using var rasterBitmap = SKBitmap.Decode(rasterPng)
                                 ?? throw new InvalidOperationException("Could not decode the PdfiumRaster PNG.");
        using var coreBitmap = SKBitmap.Decode(corePng)
                               ?? throw new InvalidOperationException("Could not decode the PDFiumCore PNG.");
        if (rasterBitmap.Width != coreBitmap.Width || rasterBitmap.Height != coreBitmap.Height)
        {
            throw new InvalidOperationException("Comparison workflows produced different PNG dimensions.");
        }
    }
}

internal sealed class ResettableCountingWriteStream : Stream
{
    internal long BytesWritten { get; private set; }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => BytesWritten;
    public override long Position
    {
        get => BytesWritten;
        set => throw new NotSupportedException();
    }

    internal void Reset()
    {
        BytesWritten = 0;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        BytesWritten = checked(BytesWritten + count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        BytesWritten = checked(BytesWritten + buffer.Length);
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
