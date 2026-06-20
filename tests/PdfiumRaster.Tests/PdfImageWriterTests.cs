using PdfiumRaster;

namespace PdfiumRaster.Tests;

public sealed class PdfImageWriterTests
{
    [Fact]
    public void WriteBmp_writes_top_down_32bpp_bmp()
    {
        var bitmap = new PdfBitmap(
            width: 1,
            height: 2,
            stride: 4,
            pixels:
            [
                0x10, 0x20, 0x30, 0xFF,
                0x40, 0x50, 0x60, 0xFF,
            ]);

        using var stream = new MemoryStream();

        PdfImageWriter.WriteBmp(bitmap, stream);

        var bytes = stream.ToArray();

        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
        Assert.Equal(62, ReadInt32(bytes, 2));
        Assert.Equal(54, ReadInt32(bytes, 10));
        Assert.Equal(40, ReadInt32(bytes, 14));
        Assert.Equal(1, ReadInt32(bytes, 18));
        Assert.Equal(-2, ReadInt32(bytes, 22));
        Assert.Equal(32, ReadUInt16(bytes, 28));
        Assert.Equal(8, ReadInt32(bytes, 34));
        Assert.Equal(bitmap.Pixels, bytes[54..]);
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        return bytes[offset]
               | (bytes[offset + 1] << 8)
               | (bytes[offset + 2] << 16)
               | (bytes[offset + 3] << 24);
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
    }
}