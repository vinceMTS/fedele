using System.Text;

namespace Fedele.Anonymizer;

/// <summary>Kind of text encoding detected in a file.</summary>
public enum EncodingKind
{
    Ascii,
    Utf8,
    Utf16LittleEndian,
    Utf16BigEndian,
    SingleByte,
}

/// <summary>
/// Encoding detected in a file, with the length of its byte order mark (0 when absent).
/// </summary>
internal sealed record DetectedEncoding(EncodingKind Kind, int BomLength)
{
    private static readonly Encoding _utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly Encoding _utf16LittleEndian = new UnicodeEncoding(bigEndian: false, byteOrderMark: false);
    private static readonly Encoding _utf16BigEndian = new UnicodeEncoding(bigEndian: true, byteOrderMark: false);

    static DetectedEncoding() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Size in bytes of one code unit: 2 for UTF-16, 1 otherwise.</summary>
    public int UnitSize => Kind is EncodingKind.Utf16LittleEndian or EncodingKind.Utf16BigEndian ? 2 : 1;

    /// <summary>True for UTF-16 big endian.</summary>
    public bool IsBigEndian => Kind == EncodingKind.Utf16BigEndian;

    /// <summary>The .NET encoding used to decode and encode values.</summary>
    public Encoding TextEncoding => Kind switch
    {
        EncodingKind.Utf16LittleEndian => _utf16LittleEndian,
        EncodingKind.Utf16BigEndian => _utf16BigEndian,
        EncodingKind.SingleByte => Encoding.GetEncoding(1252),
        _ => _utf8,
    };

    /// <summary>Human-readable name of the encoding.</summary>
    public string DisplayName => Kind switch
    {
        EncodingKind.Ascii => "ASCII (also valid UTF-8)",
        EncodingKind.Utf8 => "UTF-8",
        EncodingKind.Utf16LittleEndian => "UTF-16 LE",
        EncodingKind.Utf16BigEndian => "UTF-16 BE",
        _ => "windows-1252 (assumed: not valid UTF-8)",
    };

    /// <summary>Human-readable description of the byte order mark.</summary>
    public string BomDisplay => BomLength == 0 ? "none" : $"yes ({BomLength} bytes)";
}
