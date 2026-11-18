using System.Text;

namespace Fedele.Anonymizer;

/// <summary>
/// Detects the text encoding of a file from its bytes, reading it in chunks.
/// </summary>
internal static class EncodingDetector
{
    private const int SampleSize = 4096;
    private const int BufferSize = 64 * 1024;

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static ReadOnlySpan<byte> Utf16LittleEndianBom => [0xFF, 0xFE];

    private static ReadOnlySpan<byte> Utf16BigEndianBom => [0xFE, 0xFF];

    /// <summary>Detects the encoding of a seekable stream. Leaves the position undefined.</summary>
    public static DetectedEncoding Detect(Stream stream)
    {
        stream.Position = 0;
        Span<byte> head = stackalloc byte[3];
        head = head[..stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false)];

        if (head.StartsWith(Utf8Bom))
        {
            return new DetectedEncoding(EncodingKind.Utf8, Utf8Bom.Length);
        }

        if (head.StartsWith(Utf16LittleEndianBom))
        {
            return new DetectedEncoding(EncodingKind.Utf16LittleEndian, Utf16LittleEndianBom.Length);
        }

        if (head.StartsWith(Utf16BigEndianBom))
        {
            return new DetectedEncoding(EncodingKind.Utf16BigEndian, Utf16BigEndianBom.Length);
        }

        stream.Position = 0;
        var sample = new byte[SampleSize];
        var sampleLength = stream.ReadAtLeast(sample, SampleSize, throwOnEndOfStream: false);
        var utf16 = GuessUtf16WithoutBom(sample.AsSpan(0, sampleLength));
        if (utf16 is not null)
        {
            return utf16;
        }

        stream.Position = 0;
        return ScanUtf8(stream);
    }

    // Text in UTF-16 without BOM has a zero byte in almost every other position.
    private static DetectedEncoding? GuessUtf16WithoutBom(ReadOnlySpan<byte> sample)
    {
        var pairs = sample.Length / 2;
        if (pairs < 2)
        {
            return null;
        }

        int evenZeros = 0, oddZeros = 0;
        for (var i = 0; i + 1 < sample.Length; i += 2)
        {
            if (sample[i] == 0)
            {
                evenZeros++;
            }

            if (sample[i + 1] == 0)
            {
                oddZeros++;
            }
        }

        if (oddZeros > pairs * 0.3 && evenZeros < pairs * 0.05)
        {
            return new DetectedEncoding(EncodingKind.Utf16LittleEndian, 0);
        }

        if (evenZeros > pairs * 0.3 && oddZeros < pairs * 0.05)
        {
            return new DetectedEncoding(EncodingKind.Utf16BigEndian, 0);
        }

        return null;
    }

    // Validates the whole file as UTF-8; any invalid sequence means a single-byte encoding.
    private static DetectedEncoding ScanUtf8(Stream stream)
    {
        var decoder = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetDecoder();
        var bytes = new byte[BufferSize];
        var chars = new char[BufferSize + 4];
        var nonAscii = false;
        try
        {
            int read;
            while ((read = stream.Read(bytes, 0, bytes.Length)) > 0)
            {
                nonAscii |= bytes.AsSpan(0, read).IndexOfAnyExceptInRange((byte)0, (byte)0x7F) >= 0;
                decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
            }

            decoder.GetChars(Array.Empty<byte>(), 0, 0, chars, 0, flush: true);
        }
        catch (DecoderFallbackException)
        {
            return new DetectedEncoding(EncodingKind.SingleByte, 0);
        }

        return new DetectedEncoding(nonAscii ? EncodingKind.Utf8 : EncodingKind.Ascii, 0);
    }
}
