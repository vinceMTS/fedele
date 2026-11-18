using System.Buffers;
using System.Text;

namespace Fedele.Core;

/// <summary>A text encoding Fedele can read.</summary>
public enum TextEncodingKind
{
    /// <summary>Only bytes from 0 to 127: also valid UTF-8.</summary>
    Ascii,

    /// <summary>UTF-8.</summary>
    Utf8,

    /// <summary>UTF-16, little endian (the usual one on Windows).</summary>
    Utf16LittleEndian,

    /// <summary>UTF-16, big endian.</summary>
    Utf16BigEndian,

    /// <summary>Windows-1252, the single-byte encoding of Western European Windows and Excel.</summary>
    Windows1252,
}

/// <summary>The encoding of a text file and the length of its byte order mark.</summary>
/// <param name="Kind">The encoding.</param>
/// <param name="BomLength">Length in bytes of the byte order mark at the start of the file, 0 when absent.</param>
public sealed record TextEncodingInfo(TextEncodingKind Kind, int BomLength)
{
    private static readonly Encoding _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly Encoding _utf16LittleEndian = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
    private static readonly Encoding _utf16BigEndian = new UnicodeEncoding(bigEndian: true, byteOrderMark: false);
    private static readonly Lazy<Encoding> _windows1252 = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    });

    /// <summary>True when the file starts with a byte order mark.</summary>
    public bool HasBom => BomLength > 0;

    /// <summary>Size in bytes of one code unit: 2 for UTF-16, 1 otherwise.</summary>
    public int CodeUnitSize => Kind is TextEncodingKind.Utf16LittleEndian or TextEncodingKind.Utf16BigEndian ? 2 : 1;

    /// <summary>Stable identifier: <c>ascii</c>, <c>utf-8</c>, <c>utf-16le</c>, <c>utf-16be</c> or <c>windows-1252</c>.</summary>
    public string Id => IdOf(Kind);

    /// <summary>The .NET encoding that decodes this text (UTF-8 for ASCII). It never writes a byte order mark.</summary>
    public Encoding Encoding => Kind switch
    {
        TextEncodingKind.Utf16LittleEndian => _utf16LittleEndian,
        TextEncodingKind.Utf16BigEndian => _utf16BigEndian,
        TextEncodingKind.Windows1252 => _windows1252.Value,
        _ => _utf8,
    };

    /// <summary>Returns the stable identifier of an encoding, such as <c>utf-8</c>.</summary>
    public static string IdOf(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Ascii => "ascii",
        TextEncodingKind.Utf8 => "utf-8",
        TextEncodingKind.Utf16LittleEndian => "utf-16le",
        TextEncodingKind.Utf16BigEndian => "utf-16be",
        TextEncodingKind.Windows1252 => "windows-1252",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

/// <summary>
/// Recognizes the encoding of a text file from a sample of its first bytes.
/// It works on a sample so that it never needs to read the whole file or seek back.
/// </summary>
public static class TextEncodingDetector
{
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static ReadOnlySpan<byte> Utf16LittleEndianBom => [0xFF, 0xFE];

    private static ReadOnlySpan<byte> Utf16BigEndianBom => [0xFE, 0xFF];

    /// <summary>Recognizes the encoding of a file that starts with <paramref name="sample"/>.</summary>
    /// <param name="sample">The first bytes of the file.</param>
    /// <param name="isWholeFile">True when the sample is the whole file, so nothing can surprise later.</param>
    public static Detected<TextEncodingInfo> Detect(ReadOnlySpan<byte> sample, bool isWholeFile)
    {
        if (sample.StartsWith(Utf8Bom))
        {
            return Found(TextEncodingKind.Utf8, Utf8Bom.Length, 1.0, "The file starts with the UTF-8 byte order mark (EF BB BF).");
        }

        if (sample.StartsWith(Utf16LittleEndianBom))
        {
            return Found(TextEncodingKind.Utf16LittleEndian, 2, 1.0, "The file starts with the UTF-16 little endian byte order mark (FF FE).");
        }

        if (sample.StartsWith(Utf16BigEndianBom))
        {
            return Found(TextEncodingKind.Utf16BigEndian, 2, 1.0, "The file starts with the UTF-16 big endian byte order mark (FE FF).");
        }

        var utf16 = GuessUtf16WithoutBom(sample);
        if (utf16 is not null)
        {
            return utf16;
        }

        var firstNonAscii = sample.IndexOfAnyExceptInRange((byte)0, (byte)0x7F);
        if (firstNonAscii < 0)
        {
            return isWholeFile
                ? Found(TextEncodingKind.Ascii, 0, 1.0, "Every byte of the file is ASCII.")
                : Found(TextEncodingKind.Ascii, 0, 0.7, $"The first {sample.Length} bytes are all ASCII; later bytes may still be UTF-8 or windows-1252.");
        }

        var (valid, multiByte, invalidOffset) = ScanUtf8(sample[firstNonAscii..], isWholeFile);
        if (!valid)
        {
            return Found(
                TextEncodingKind.Windows1252,
                0,
                0.8,
                $"The byte at offset {firstNonAscii + invalidOffset} is not valid UTF-8; windows-1252 assumed, the most common single-byte encoding in Italian files.");
        }

        var confidence = multiByte >= 3 ? 0.99 : 0.9;
        return Found(TextEncodingKind.Utf8, 0, confidence, $"{multiByte} multi-byte sequences, all valid UTF-8.");
    }

    /// <summary>
    /// Uses the encoding chosen by the caller, and measures its byte order mark in <paramref name="sample"/>.
    /// A byte order mark of another encoding is not skipped: its bytes are part of the text.
    /// </summary>
    public static Detected<TextEncodingInfo> FromOptions(TextEncodingKind kind, ReadOnlySpan<byte> sample)
    {
        var bomLength = kind switch
        {
            TextEncodingKind.Utf8 when sample.StartsWith(Utf8Bom) => Utf8Bom.Length,
            TextEncodingKind.Utf16LittleEndian when sample.StartsWith(Utf16LittleEndianBom) => 2,
            TextEncodingKind.Utf16BigEndian when sample.StartsWith(Utf16BigEndianBom) => 2,
            _ => 0,
        };
        return Found(kind, bomLength, 1.0, "Set in the read options.");
    }

    private static Detected<TextEncodingInfo> Found(TextEncodingKind kind, int bomLength, double confidence, string reason) =>
        new(new TextEncodingInfo(kind, bomLength), confidence, reason);

    // Text in UTF-16 without a byte order mark has a zero byte in almost every other position.
    private static Detected<TextEncodingInfo>? GuessUtf16WithoutBom(ReadOnlySpan<byte> sample)
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
            return Found(TextEncodingKind.Utf16LittleEndian, 0, 0.9, "No byte order mark, but a zero byte follows almost every other byte, as in UTF-16 little endian.");
        }

        if (evenZeros > pairs * 0.3 && oddZeros < pairs * 0.05)
        {
            return Found(TextEncodingKind.Utf16BigEndian, 0, 0.9, "No byte order mark, but a zero byte precedes almost every other byte, as in UTF-16 big endian.");
        }

        return null;
    }

    // A sequence cut by the end of an incomplete sample is not an error: the rest is in the next bytes.
    private static (bool Valid, int MultiByte, int InvalidOffset) ScanUtf8(ReadOnlySpan<byte> bytes, bool isWholeFile)
    {
        var multiByte = 0;
        var offset = 0;
        while (offset < bytes.Length)
        {
            var status = Rune.DecodeFromUtf8(bytes[offset..], out _, out var consumed);
            if (status == OperationStatus.NeedMoreData && !isWholeFile)
            {
                break;
            }

            if (status != OperationStatus.Done)
            {
                return (false, multiByte, offset);
            }

            if (consumed > 1)
            {
                multiByte++;
            }

            offset += consumed;
        }

        return (true, multiByte, -1);
    }
}
