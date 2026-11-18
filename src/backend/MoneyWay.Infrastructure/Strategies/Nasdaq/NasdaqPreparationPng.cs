using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace MoneyWay.Infrastructure.Strategies.Nasdaq;

// Narrow screenshot policy, not an image renderer. Bytes are validated, never rewritten.
public static class NasdaqPreparationPng
{
    public const int MaximumBytes = 4 * 1024 * 1024;
    public const int MaximumPixels = 16 * 1024 * 1024;
    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(i =>
    { uint c = (uint)i; for (var n = 0; n < 8; n++) c = (c & 1) != 0 ? 0xedb88320U ^ (c >> 1) : c >> 1; return c; }).ToArray();
    public static void Validate(byte[] bytes)
    {
        static void Need(bool valid) { if (!valid) throw new ArgumentException("Invalid or unsupported PNG: bounded noninterlaced 8-bit RGB/RGBA screenshot required."); }
        Need(bytes.Length is >= 57 and <= MaximumBytes && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        var position = 8; var chunks = 0; var width = 0; var height = 0; var channels = 0; var ended = false; var dataEnded = false;
        using var compressed = new MemoryStream();
        while (position < bytes.Length)
        {
            Need(++chunks <= 4096 && bytes.Length - position >= 12);
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4)); Need(size <= (uint)(bytes.Length - position - 12));
            int length = (int)size; var type = Encoding.ASCII.GetString(bytes, position + 4, 4);
            uint crc = uint.MaxValue; for (var n = position + 4; n < position + 8 + length; n++) crc = CrcTable[(crc ^ bytes[n]) & 255] ^ (crc >> 8);
            Need((crc ^ uint.MaxValue) == BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position + 8 + length, 4)));
            var data = bytes.AsSpan(position + 8, length);
            if (chunks == 1)
            {
                Need(type == "IHDR" && length == 13);
                var w = BinaryPrimitives.ReadUInt32BigEndian(data); var h = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
                Need(w > 0 && h > 0 && w <= 16384 && h <= 16384 && (ulong)w * h <= MaximumPixels);
                width = (int)w; height = (int)h;
                Need(data[8] == 8 && data[9] is 2 or 6 && data[10] == 0 && data[11] == 0 && data[12] == 0); channels = data[9] == 2 ? 3 : 4;
            }
            else if (type == "IDAT") { Need(!dataEnded); compressed.Write(data); }
            else if (type == "IEND") { Need(length == 0 && compressed.Length > 0); ended = true; }
            else { Need(type is "sRGB" or "gAMA" or "cHRM" or "pHYs" or "tEXt" or "iTXt" or "zTXt" or "bKGD" or "sBIT" or "tIME" or "iCCP"); if (compressed.Length > 0) dataEnded = true; }
            position += length + 12; if (ended) break;
        }
        Need(ended && position == bytes.Length);
        compressed.Position = 0;
        try
        {
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            var row = new byte[checked(width * channels)];
            for (var y = 0; y < height; y++) { var filter = zlib.ReadByte(); Need(filter is >= 0 and <= 4); zlib.ReadExactly(row); }
            Need(zlib.ReadByte() == -1);
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException) { throw new ArgumentException("Invalid PNG pixel stream.", e); }
    }
}
