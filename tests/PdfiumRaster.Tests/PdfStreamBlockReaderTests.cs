using System.Buffers;
using System.Runtime.InteropServices;
using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfStreamBlockReaderTests
{
    [Fact]
    public void Read_chunks_large_blocks_and_reuses_one_buffer()
    {
        var requestedSize = checked((PdfStreamBlockReader.MaximumBufferSize * 2) + 123);
        var sourceBytes = Enumerable.Range(0, requestedSize + 19)
            .Select(index => unchecked((byte)(index * 31)))
            .ToArray();
        using var stream = new PartialReadStream(sourceBytes, maxBytesPerRead: 7_000);
        var pool = new RecordingArrayPool();
        var destination = Marshal.AllocHGlobal(requestedSize);

        try
        {
            using (var reader = new PdfStreamBlockReader(stream, pool))
            {
                Assert.True(reader.Read(position: 11, requestedSize, destination));

                var actual = new byte[requestedSize];
                Marshal.Copy(destination, actual, 0, actual.Length);

                Assert.Equal(sourceBytes.Skip(11).Take(requestedSize), actual);
                Assert.Equal(1, pool.RentCount);
                Assert.True(stream.MaximumRequestedCount <= PdfStreamBlockReader.MaximumBufferSize);

                Assert.True(reader.Read(position: 0, PdfStreamBlockReader.MaximumBufferSize + 1, destination));
                Assert.Equal(1, pool.RentCount);
            }

            Assert.Equal(1, pool.ReturnCount);
        }
        finally
        {
            Marshal.FreeHGlobal(destination);
        }
    }

    [Fact]
    public void Read_returns_false_when_stream_ends_before_requested_block()
    {
        using var stream = new MemoryStream(new byte[32], writable: false);
        using var reader = new PdfStreamBlockReader(stream);
        var destination = Marshal.AllocHGlobal(64);

        try
        {
            Assert.False(reader.Read(position: 0, size: 64, destination));
        }
        finally
        {
            Marshal.FreeHGlobal(destination);
        }
    }

    [Fact]
    public void Dispose_returns_buffer_once_and_rejects_further_reads()
    {
        using var stream = new MemoryStream(new byte[1], writable: false);
        var pool = new RecordingArrayPool();
        var reader = new PdfStreamBlockReader(stream, pool);
        var destination = Marshal.AllocHGlobal(1);

        try
        {
            Assert.True(reader.Read(position: 0, size: 1, destination));

            reader.Dispose();
            reader.Dispose();

            Assert.Equal(1, pool.RentCount);
            Assert.Equal(1, pool.ReturnCount);
            Assert.Throws<ObjectDisposedException>(() => reader.Read(position: 0, size: 1, destination));
        }
        finally
        {
            Marshal.FreeHGlobal(destination);
        }
    }

    private sealed class RecordingArrayPool : ArrayPool<byte>
    {
        internal int RentCount { get; private set; }
        internal int ReturnCount { get; private set; }

        public override byte[] Rent(int minimumLength)
        {
            RentCount++;
            return new byte[minimumLength];
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            ReturnCount++;
        }
    }

    private sealed class PartialReadStream : MemoryStream
    {
        private readonly int _maxBytesPerRead;

        internal PartialReadStream(byte[] bytes, int maxBytesPerRead)
            : base(bytes, writable: false)
        {
            _maxBytesPerRead = maxBytesPerRead;
        }

        internal int MaximumRequestedCount { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            MaximumRequestedCount = Math.Max(MaximumRequestedCount, count);
            return base.Read(buffer, offset, Math.Min(count, _maxBytesPerRead));
        }
    }
}
