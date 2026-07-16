using System.Buffers;
using System.Runtime.InteropServices;

namespace PdfiumRaster;

internal sealed class PdfStreamBlockReader : IDisposable
{
    internal const int MaximumBufferSize = 64 * 1024;

    // PDFium calls, including custom file callbacks, are serialized process-wide. Sharing one scratch buffer avoids
    // retaining one buffer per open stream-backed document while keeping the maximum temporary copy size bounded.
    private static readonly PooledBuffer SharedBuffer = new(ArrayPool<byte>.Shared);

    private readonly Stream _stream;
    private readonly PooledBuffer _pooledBuffer;
    private readonly bool _ownsPooledBuffer;
    private bool _disposed;

    internal PdfStreamBlockReader(Stream stream, ArrayPool<byte>? bufferPool = null)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _pooledBuffer = bufferPool is null ? SharedBuffer : new PooledBuffer(bufferPool);
        _ownsPooledBuffer = bufferPool is not null;
    }

    internal bool Read(ulong position, int size, IntPtr destination)
    {
        if (position > long.MaxValue || size < 0 || (size > 0 && destination == IntPtr.Zero))
        {
            return false;
        }

        lock (_pooledBuffer.SyncRoot)
        {
            ThrowIfDisposed();

            if (size == 0)
            {
                return true;
            }

            var buffer = _pooledBuffer.GetBuffer();
            _stream.Position = checked((long)position);

            var totalRead = 0;
            while (totalRead < size)
            {
                var chunkSize = Math.Min(size - totalRead, MaximumBufferSize);
                var chunkRead = 0;

                while (chunkRead < chunkSize)
                {
                    var read = _stream.Read(buffer, chunkRead, chunkSize - chunkRead);
                    if (read == 0)
                    {
                        return false;
                    }

                    chunkRead += read;
                }

                Marshal.Copy(buffer, 0, IntPtr.Add(destination, totalRead), chunkSize);
                totalRead += chunkSize;
            }

            return true;
        }
    }

    public void Dispose()
    {
        lock (_pooledBuffer.SyncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_ownsPooledBuffer)
            {
                _pooledBuffer.ReturnBuffer();
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private sealed class PooledBuffer
    {
        private readonly ArrayPool<byte> _pool;
        private byte[]? _buffer;

        internal PooledBuffer(ArrayPool<byte> pool)
        {
            _pool = pool;
        }

        internal object SyncRoot { get; } = new();

        internal byte[] GetBuffer()
        {
            return _buffer ??= _pool.Rent(MaximumBufferSize);
        }

        internal void ReturnBuffer()
        {
            if (_buffer is null)
            {
                return;
            }

            _pool.Return(_buffer);
            _buffer = null;
        }
    }
}
