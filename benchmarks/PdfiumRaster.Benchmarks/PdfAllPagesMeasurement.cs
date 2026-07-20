using System.Diagnostics;
using System.Globalization;
using PdfiumRaster;

namespace PdfiumRaster.Benchmarks;

internal static class PdfAllPagesMeasurement
{
    internal static void Run(string[] args)
    {
        if (args.Length != 4)
        {
            throw new ArgumentException(
                "Expected arguments: <pdf-path> <managed|native> <dpi> <Bmp|Png|Jpeg|Webp>.");
        }

        var pdfPath = Path.GetFullPath(args[0]);
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("The measurement PDF could not be found.", pdfPath);
        }

        var useNativeBuffer = args[1].ToLowerInvariant() switch
        {
            "managed" => false,
            "native" => true,
            _ => throw new ArgumentException("Buffer mode must be 'managed' or 'native'."),
        };
        var dpi = int.Parse(args[2], NumberStyles.None, CultureInfo.InvariantCulture);
        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(args), dpi, "DPI must be greater than zero.");
        }

        var format = Enum.Parse<PdfImageOutputFormat>(args[3], ignoreCase: true);
        if (!Enum.IsDefined(typeof(PdfImageOutputFormat), format))
        {
            throw new ArgumentOutOfRangeException(nameof(args), format, "Image format is not supported.");
        }
        var options = new PdfImageConversionOptions
        {
            Format = format,
            Render = new PdfPageRenderOptions { Dpi = dpi },
            Encoding = PdfImageEncodingOptions.Fast,
        };

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var startingWorkingSet = process.WorkingSet64;
        using var samplingCancellation = new CancellationTokenSource();
        var workingSetSampler = SamplePeakWorkingSetAsync(startingWorkingSet, samplingCancellation.Token);
        var startingAllocatedBytes = GC.GetTotalAllocatedBytes(precise: true);
        var startingGen0 = GC.CollectionCount(0);
        var startingGen1 = GC.CollectionCount(1);
        var startingGen2 = GC.CollectionCount(2);
        var stopwatch = Stopwatch.StartNew();

        var result = useNativeBuffer
            ? MeasureNative(pdfPath, options)
            : MeasureManaged(pdfPath, options);

        stopwatch.Stop();
        samplingCancellation.Cancel();
        var sampledPeakWorkingSet = workingSetSampler.GetAwaiter().GetResult();
        var peakWorkingSetDelta = Math.Max(0, sampledPeakWorkingSet - startingWorkingSet);
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - startingAllocatedBytes;

        Console.WriteLine(string.Join(",",
            useNativeBuffer ? "native" : "managed",
            dpi.ToString(CultureInfo.InvariantCulture),
            format,
            result.PageCount.ToString(CultureInfo.InvariantCulture),
            stopwatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture),
            result.EncodedBytes.ToString(CultureInfo.InvariantCulture),
            result.TotalPixelBytes.ToString(CultureInfo.InvariantCulture),
            result.MaximumPageBufferBytes.ToString(CultureInfo.InvariantCulture),
            allocatedBytes.ToString(CultureInfo.InvariantCulture),
            peakWorkingSetDelta.ToString(CultureInfo.InvariantCulture),
            (GC.CollectionCount(0) - startingGen0).ToString(CultureInfo.InvariantCulture),
            (GC.CollectionCount(1) - startingGen1).ToString(CultureInfo.InvariantCulture),
            (GC.CollectionCount(2) - startingGen2).ToString(CultureInfo.InvariantCulture)));
    }

    private static async Task<long> SamplePeakWorkingSetAsync(long startingWorkingSet, CancellationToken cancellationToken)
    {
        var peakWorkingSet = startingWorkingSet;
        using var process = Process.GetCurrentProcess();

        while (!cancellationToken.IsCancellationRequested)
        {
            process.Refresh();
            peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
            await Task.Delay(10).ConfigureAwait(false);
        }

        process.Refresh();
        return Math.Max(peakWorkingSet, process.WorkingSet64);
    }

    private static MeasurementResult MeasureManaged(string pdfPath, PdfImageConversionOptions options)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath);
        PdfBitmapLease? lease = null;
        long encodedBytes = 0;
        long totalPixelBytes = 0;
        long maximumPageBufferBytes = 0;

        try
        {
            for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
            {
                using var page = document.LoadPage(pageIndex);
                var renderOptions = PdfImageConverter.GetRenderOptions(options);
                lease = PdfImageConverter.EnsureBitmapLease(lease, page, renderOptions);
                var bitmap = PdfImageConverter.RenderToLease(page, lease, renderOptions, options);
                var pageBufferBytes = checked((long)bitmap.Stride * bitmap.Height);
                totalPixelBytes = checked(totalPixelBytes + pageBufferBytes);
                maximumPageBufferBytes = Math.Max(maximumPageBufferBytes, pageBufferBytes);

                using var output = new CountingWriteStream();
                PdfImageConverter.SaveBitmap(bitmap, output, options.Format, options.Encoding);
                encodedBytes = checked(encodedBytes + output.BytesWritten);
            }
        }
        finally
        {
            lease?.Dispose();
        }

        return new MeasurementResult(document.PageCount, encodedBytes, totalPixelBytes, maximumPageBufferBytes);
    }

    private static MeasurementResult MeasureNative(string pdfPath, PdfImageConversionOptions options)
    {
        using var library = PdfiumLibrary.Initialize();
        using var document = PdfDocument.Load(pdfPath);
        PdfNativeBitmapLease? lease = null;
        long encodedBytes = 0;
        long totalPixelBytes = 0;
        long maximumPageBufferBytes = 0;

        try
        {
            for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
            {
                using var page = document.LoadPage(pageIndex);
                var renderOptions = PdfImageConverter.GetRenderOptions(options);
                lease = PdfImageConverter.EnsureNativeBitmapLease(lease, page, renderOptions);
                PdfImageConverter.RenderToLease(page, lease, renderOptions, options);
                var pageBufferBytes = checked((long)lease.Stride * lease.Height);
                totalPixelBytes = checked(totalPixelBytes + pageBufferBytes);
                maximumPageBufferBytes = Math.Max(maximumPageBufferBytes, pageBufferBytes);

                using var output = new CountingWriteStream();
                PdfImageConverter.SaveBitmap(lease, output, options.Format, options.Encoding);
                encodedBytes = checked(encodedBytes + output.BytesWritten);
            }
        }
        finally
        {
            lease?.Dispose();
        }

        return new MeasurementResult(document.PageCount, encodedBytes, totalPixelBytes, maximumPageBufferBytes);
    }

    private readonly record struct MeasurementResult(
        int PageCount,
        long EncodedBytes,
        long TotalPixelBytes,
        long MaximumPageBufferBytes);
}
