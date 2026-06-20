using System.Buffers;
using System.Runtime.InteropServices;

namespace PdfiumRaster;

/// <summary>
/// Represents an open PDF document.
/// </summary>
public sealed class PdfDocument : IDisposable
{
    private IntPtr _handle;
    private GCHandle _pinnedBytes;
    private PdfStreamAccess? _streamAccess;

    private PdfDocument(IntPtr handle, GCHandle pinnedBytes = default, PdfStreamAccess? streamAccess = null)
    {
        _handle = handle;
        _pinnedBytes = pinnedBytes;
        _streamAccess = streamAccess;
    }

    /// <summary>
    /// Gets the number of pages in the document.
    /// </summary>
    public int PageCount
    {
        get
        {
            ThrowIfDisposed();
            return PdfiumNative.FPDF_GetPageCount(_handle);
        }
    }

    /// <summary>
    /// Opens a PDF document from a file path.
    /// </summary>
    /// <param name="path">Path to the PDF file.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>An open PDF document.</returns>
    public static PdfDocument Load(string path, string? password = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
        }

        var handle = PdfiumNative.FPDF_LoadDocument(path, password);
        if (handle == IntPtr.Zero)
        {
            throw PdfiumException.FromLastError($"Could not load PDF document '{path}'.");
        }

        return new PdfDocument(handle);
    }

    /// <summary>
    /// Opens a PDF document from a byte array.
    /// </summary>
    /// <param name="bytes">PDF file bytes.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>An open PDF document.</returns>
    public static PdfDocument Load(byte[] bytes, string? password = null)
    {
        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        if (bytes.Length == 0)
        {
            throw new ArgumentException("PDF bytes cannot be empty.", nameof(bytes));
        }

        var pinnedBytes = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        var handle = PdfiumNative.FPDF_LoadMemDocument(pinnedBytes.AddrOfPinnedObject(), bytes.Length, password);

        if (handle == IntPtr.Zero)
        {
            pinnedBytes.Free();
            throw PdfiumException.FromLastError("Could not load PDF document from memory.");
        }

        return new PdfDocument(handle, pinnedBytes);
    }

    /// <summary>
    /// Opens a PDF document from a stream.
    /// </summary>
    /// <param name="stream">Stream containing PDF file data. Seekable streams are read through PDFium custom file access without copying the full stream into memory.</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="stream"/> open after the document is disposed.</param>
    /// <param name="password">Optional document password.</param>
    /// <returns>An open PDF document.</returns>
    public static PdfDocument Load(Stream stream, bool leaveOpen = false, string? password = null)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (stream.CanSeek)
        {
            return LoadSeekableStream(stream, leaveOpen, password);
        }

        try
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return Load(memoryStream.ToArray(), password);
        }
        finally
        {
            if (!leaveOpen)
            {
                stream.Dispose();
            }
        }
    }

    private static PdfDocument LoadSeekableStream(Stream stream, bool leaveOpen, string? password)
    {
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        var streamAccess = new PdfStreamAccess(stream, leaveOpen);

        try
        {
            var handle = streamAccess.LoadDocument(password);
            if (handle == IntPtr.Zero)
            {
                throw PdfiumException.FromLastError("Could not load PDF document from stream.");
            }

            return new PdfDocument(handle, streamAccess: streamAccess);
        }
        catch
        {
            streamAccess.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Loads a single page by zero-based page index.
    /// </summary>
    /// <param name="pageIndex">Zero-based page index.</param>
    /// <returns>An open PDF page.</returns>
    public PdfPage LoadPage(int pageIndex)
    {
        ThrowIfDisposed();

        if (pageIndex < 0 || pageIndex >= PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex,
                "Page index is outside the document page range.");
        }

        var pageHandle = PdfiumNative.FPDF_LoadPage(_handle, pageIndex);
        if (pageHandle == IntPtr.Zero)
        {
            throw PdfiumException.FromLastError($"Could not load PDF page {pageIndex}.");
        }

        return new PdfPage(pageHandle);
    }

    /// <summary>
    /// Closes the document and releases associated native resources.
    /// </summary>
    public void Dispose()
    {
        var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero)
        {
            PdfiumNative.FPDF_CloseDocument(handle);
        }

        if (_pinnedBytes.IsAllocated)
        {
            _pinnedBytes.Free();
        }

        _streamAccess?.Dispose();
        _streamAccess = null;
    }

    private void ThrowIfDisposed()
    {
        if (_handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private sealed class PdfStreamAccess : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly object _syncRoot = new();
        private readonly PdfiumNative.GetBlock32Callback _getBlock32;
        private readonly PdfiumNative.GetBlock64Callback _getBlock64;
        private PdfiumNative.FileAccess32 _fileAccess32;
        private PdfiumNative.FileAccess64 _fileAccess64;
        private IntPtr _fileAccessPointer;
        private GCHandle _stateHandle;
        private bool _disposed;

        internal PdfStreamAccess(Stream stream, bool leaveOpen)
        {
            _stream = stream;
            _leaveOpen = leaveOpen;
            _getBlock32 = ReadBlock32;
            _getBlock64 = ReadBlock64;
            _stateHandle = GCHandle.Alloc(this);
        }

        internal IntPtr LoadDocument(string? password)
        {
            var length = _stream.Length;
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(_stream), length, "Stream length must not be negative.");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (length > uint.MaxValue)
                {
                    throw new NotSupportedException(
                        "PDFium custom stream access is limited to 4 GB on Windows. Use a file path for larger PDFs.");
                }

                _fileAccess32 = new PdfiumNative.FileAccess32
                {
                    FileLength = checked((uint)length),
                    GetBlock = _getBlock32,
                    Param = GCHandle.ToIntPtr(_stateHandle),
                };

                _fileAccessPointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PdfiumNative.FileAccess32)));
                Marshal.StructureToPtr(_fileAccess32, _fileAccessPointer, false);

                return PdfiumNative.FPDF_LoadCustomDocument(_fileAccessPointer, password);
            }

            _fileAccess64 = new PdfiumNative.FileAccess64
            {
                FileLength = checked((ulong)length),
                GetBlock = _getBlock64,
                Param = GCHandle.ToIntPtr(_stateHandle),
            };

            _fileAccessPointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(PdfiumNative.FileAccess64)));
            Marshal.StructureToPtr(_fileAccess64, _fileAccessPointer, false);

            return PdfiumNative.FPDF_LoadCustomDocument(_fileAccessPointer, password);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_stateHandle.IsAllocated)
            {
                _stateHandle.Free();
            }

            if (_fileAccessPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_fileAccessPointer);
                _fileAccessPointer = IntPtr.Zero;
            }

            if (!_leaveOpen)
            {
                _stream.Dispose();
            }

            _disposed = true;
        }

        private static int ReadBlock32(IntPtr param, uint position, IntPtr buffer, uint size)
        {
            return ReadBlock(param, position, size, buffer);
        }

        private static int ReadBlock64(IntPtr param, ulong position, IntPtr buffer, ulong size)
        {
            return position > long.MaxValue || size > int.MaxValue
                ? 0
                : ReadBlock(param, position, size, buffer);
        }

        private static int ReadBlock(IntPtr param, ulong position, ulong size, IntPtr buffer)
        {
            if (param == IntPtr.Zero || buffer == IntPtr.Zero || size > int.MaxValue)
            {
                return 0;
            }

            var handle = GCHandle.FromIntPtr(param);
            if (handle.Target is not PdfStreamAccess access || access._disposed)
            {
                return 0;
            }

            return access.Read(position, checked((int)size), buffer) ? 1 : 0;
        }

        private bool Read(ulong position, int size, IntPtr buffer)
        {
            if (position > long.MaxValue)
            {
                return false;
            }

            var bytes = ArrayPool<byte>.Shared.Rent(size);
            var totalRead = 0;

            try
            {
                lock (_syncRoot)
                {
                    _stream.Position = checked((long)position);

                    while (totalRead < size)
                    {
                        var read = _stream.Read(bytes, totalRead, size - totalRead);
                        if (read == 0)
                        {
                            return false;
                        }

                        totalRead += read;
                    }
                }

                Marshal.Copy(bytes, 0, buffer, size);
                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
    }
}
