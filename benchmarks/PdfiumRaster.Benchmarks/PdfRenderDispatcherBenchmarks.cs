using BenchmarkDotNet.Attributes;
using PdfiumRaster;

namespace PdfiumRaster.Benchmarks;

/// <summary>
/// Compares sequential compressed image output with the dispatcher's serialized-render/parallel-encode pipeline.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PdfRenderDispatcherSaveBenchmarks
{
    private string _pdfPath = string.Empty;
    private PdfImageConversionOptions _options = null!;
    private PdfRenderDispatcher _dispatcher = null!;

    /// <summary>
    /// Gets or sets the number of independent requests in one measured batch.
    /// </summary>
    [Params(4)]
    public int BatchSize { get; set; }

    /// <summary>
    /// Initializes the shared dispatcher and render settings.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdfPath = GetTestPdfPath("axf-annotation-1.pdf");
        _options = new PdfImageConversionOptions
        {
            Render = PdfPageRenderOptions.ScreenPreview,
            Format = PdfImageOutputFormat.Png,
            Encoding = PdfImageEncodingOptions.Fast,
        };
        _dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions
        {
            QueueCapacity = BatchSize,
            EncodingConcurrency = 2,
        });
    }

    /// <summary>
    /// Drains and disposes the dispatcher.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _dispatcher.CompleteAsync().GetAwaiter().GetResult();
        _dispatcher.Dispose();
    }

    /// <summary>
    /// Saves the batch sequentially through the synchronous converter.
    /// </summary>
    /// <returns>Total encoded bytes.</returns>
    [Benchmark(Baseline = true)]
    public long SequentialFastPng()
    {
        long total = 0;
        for (var index = 0; index < BatchSize; index++)
        {
            using var output = new CountingWriteStream();
            PdfImageConverter.SavePage(_pdfPath, 0, output, _options);
            total += output.BytesWritten;
        }

        return total;
    }

    /// <summary>
    /// Saves the batch through one dispatcher with two encoding workers.
    /// </summary>
    /// <returns>Total encoded bytes.</returns>
    [Benchmark]
    public async Task<long> DispatcherFastPng()
    {
        var outputs = new CountingWriteStream[BatchSize];
        var tasks = new Task[BatchSize];
        try
        {
            for (var index = 0; index < BatchSize; index++)
            {
                outputs[index] = new CountingWriteStream();
                tasks[index] = _dispatcher.SavePageAsync(_pdfPath, 0, outputs[index], _options);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return outputs.Sum(output => output.BytesWritten);
        }
        finally
        {
            foreach (var output in outputs)
            {
                output?.Dispose();
            }
        }
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
/// Demonstrates that the in-process dispatcher does not parallelize the PDFium render stage.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PdfRenderDispatcherRawBenchmarks
{
    private string _pdfPath = string.Empty;
    private PdfImageConversionOptions _options = null!;
    private PdfRenderDispatcher _dispatcher = null!;

    /// <summary>
    /// Gets or sets the number of independent requests in one measured batch.
    /// </summary>
    [Params(4)]
    public int BatchSize { get; set; }

    /// <summary>
    /// Initializes the shared dispatcher and render settings.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdfPath = GetTestPdfPath("axf-annotation-1.pdf");
        _options = new PdfImageConversionOptions { Render = PdfPageRenderOptions.ScreenPreview };
        _dispatcher = new PdfRenderDispatcher(new PdfRenderDispatcherOptions { QueueCapacity = BatchSize });
    }

    /// <summary>
    /// Drains and disposes the dispatcher.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _dispatcher.CompleteAsync().GetAwaiter().GetResult();
        _dispatcher.Dispose();
    }

    /// <summary>
    /// Renders the batch sequentially.
    /// </summary>
    /// <returns>Total rendered pixels.</returns>
    [Benchmark(Baseline = true)]
    public long SequentialRaw()
    {
        long total = 0;
        for (var index = 0; index < BatchSize; index++)
        {
            var bitmap = PdfImageConverter.RenderPage(_pdfPath, 0, _options);
            total += (long)bitmap.Width * bitmap.Height;
        }

        return total;
    }

    /// <summary>
    /// Submits the batch concurrently while the dispatcher serializes PDFium.
    /// </summary>
    /// <returns>Total rendered pixels.</returns>
    [Benchmark]
    public async Task<long> DispatcherRaw()
    {
        var tasks = new Task<PdfBitmap>[BatchSize];
        for (var index = 0; index < BatchSize; index++)
        {
            tasks[index] = _dispatcher.RenderPageAsync(_pdfPath, 0, _options);
        }

        var bitmaps = await Task.WhenAll(tasks).ConfigureAwait(false);
        return bitmaps.Sum(bitmap => (long)bitmap.Width * bitmap.Height);
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

internal sealed class CountingWriteStream : Stream
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

    public override void Write(byte[] buffer, int offset, int count)
    {
        BytesWritten = checked(BytesWritten + count);
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
