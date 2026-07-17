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

/// <summary>
/// Measures batches of independent seekable-stream inputs saved through the dispatcher.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PdfRenderDispatcherStreamBenchmarks
{
    private byte[] _pdfBytes = [];
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
        _pdfBytes = File.ReadAllBytes(GetTestPdfPath("axf-annotation-1.pdf"));
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
    /// Saves independent seekable streams as fast PNG images.
    /// </summary>
    /// <returns>Total encoded bytes.</returns>
    [Benchmark(Baseline = true)]
    public async Task<long> DispatcherSeekableStreamFastPng()
    {
        var inputs = new MemoryStream[BatchSize];
        var outputs = new CountingWriteStream[BatchSize];
        var tasks = new Task[BatchSize];
        try
        {
            for (var index = 0; index < BatchSize; index++)
            {
                inputs[index] = new MemoryStream(_pdfBytes, writable: false);
                outputs[index] = new CountingWriteStream();
                tasks[index] = _dispatcher.SavePageAsync(inputs[index], 0, outputs[index], _options);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return outputs.Sum(output => output.BytesWritten);
        }
        finally
        {
            foreach (var input in inputs)
            {
                input?.Dispose();
            }

            foreach (var output in outputs)
            {
                output?.Dispose();
            }
        }
    }

    /// <summary>
    /// Saves independent non-seekable streams as fast PNG images.
    /// </summary>
    /// <returns>Total encoded bytes.</returns>
    [Benchmark]
    public async Task<long> DispatcherNonSeekableStreamFastPng()
    {
        var inputs = new NonSeekableReadStream[BatchSize];
        var outputs = new CountingWriteStream[BatchSize];
        var tasks = new Task[BatchSize];
        try
        {
            for (var index = 0; index < BatchSize; index++)
            {
                inputs[index] = new NonSeekableReadStream(_pdfBytes);
                outputs[index] = new CountingWriteStream();
                tasks[index] = _dispatcher.SavePageAsync(inputs[index], 0, outputs[index], _options);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return outputs.Sum(output => output.BytesWritten);
        }
        finally
        {
            foreach (var input in inputs)
            {
                input?.Dispose();
            }

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
/// Measures the managed allocation required to load a non-seekable PDF stream.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class PdfRenderDispatcherNonSeekableLoadBenchmarks
{
    private byte[] _pdfBytes = [];
    private PdfiumLibrary _library = null!;

    /// <summary>
    /// Loads the benchmark PDF and initializes PDFium.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdfBytes = File.ReadAllBytes(GetTestPdfPath("axf-annotation-1.pdf"));
        _library = PdfiumLibrary.Initialize();
    }

    /// <summary>
    /// Releases the PDFium initialization reference.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _library.Dispose();
    }

    /// <summary>
    /// Loads and closes one document from a non-seekable input stream.
    /// </summary>
    /// <returns>The loaded document page count.</returns>
    [Benchmark]
    public int LoadNonSeekableStream()
    {
        using var stream = new NonSeekableReadStream(_pdfBytes);
        using var document = PdfDocument.Load(stream);
        return document.PageCount;
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
/// Compares row-by-row BMP pixel output with one contiguous pixel write.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class BmpWriterBenchmarks
{
    private PdfBitmap _bitmap = null!;

    /// <summary>
    /// Creates a representative rendered-page bitmap.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _bitmap = PdfBitmap.Create(width: 1200, height: 1600);
    }

    /// <summary>
    /// Writes the bitmap header followed by one stream write per pixel row.
    /// </summary>
    /// <returns>Total bytes written.</returns>
    [Benchmark(Baseline = true)]
    public long RowByRowPixels()
    {
        using var stream = new CountingWriteStream();
        var header = new byte[54];
        stream.Write(header, 0, header.Length);

        for (var row = 0; row < _bitmap.Height; row++)
        {
            stream.Write(_bitmap.Pixels, row * _bitmap.Stride, _bitmap.Stride);
        }

        GC.KeepAlive(header);
        return stream.BytesWritten;
    }

    /// <summary>
    /// Writes the bitmap through the optimized contiguous BMP writer.
    /// </summary>
    /// <returns>Total bytes written.</returns>
    [Benchmark]
    public long ContiguousPixels()
    {
        using var stream = new CountingWriteStream();
        PdfImageWriter.WriteBmp(_bitmap, stream);
        return stream.BytesWritten;
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

internal sealed class NonSeekableReadStream : Stream
{
    private readonly MemoryStream _inner;

    internal NonSeekableReadStream(byte[] bytes)
    {
        _inner = new MemoryStream(bytes, writable: false);
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
