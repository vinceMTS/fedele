using System.Text;

namespace Fedele.Core.Tests;

public sealed class TextEncodingDetectorTests
{
    private static readonly Encoding _windows1252 = new TextEncodingInfo(TextEncodingKind.Windows1252, 0).Encoding;

    [Fact]
    public void Detect_Utf8Bom_IsCertainUtf8WithBom()
    {
        var detected = TextEncodingDetector.Detect([0xEF, 0xBB, 0xBF, (byte)'a'], isWholeFile: true);

        Assert.Equal(new TextEncodingInfo(TextEncodingKind.Utf8, 3), detected.Value);
        Assert.Equal(1.0, detected.Confidence);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, (byte)'a', 0 }, TextEncodingKind.Utf16LittleEndian)]
    [InlineData(new byte[] { 0xFE, 0xFF, 0, (byte)'a' }, TextEncodingKind.Utf16BigEndian)]
    public void Detect_Utf16Bom_IsUtf16WithBom(byte[] bytes, TextEncodingKind expected)
    {
        var detected = TextEncodingDetector.Detect(bytes, isWholeFile: true);

        Assert.Equal(new TextEncodingInfo(expected, 2), detected.Value);
    }

    [Theory]
    [InlineData(false, TextEncodingKind.Utf16LittleEndian)]
    [InlineData(true, TextEncodingKind.Utf16BigEndian)]
    public void Detect_Utf16WithoutBom_RecognizesByteOrder(bool bigEndian, TextEncodingKind expected)
    {
        var bytes = new UnicodeEncoding(bigEndian, byteOrderMark: false).GetBytes("Nome;Città\r\nRossi;Milano\r\n");

        var detected = TextEncodingDetector.Detect(bytes, isWholeFile: true);

        Assert.Equal(new TextEncodingInfo(expected, 0), detected.Value);
    }

    [Fact]
    public void Detect_AsciiWholeFile_IsCertainAscii()
    {
        var detected = TextEncodingDetector.Detect("a;b\r\n1;2\r\n"u8, isWholeFile: true);

        Assert.Equal(TextEncodingKind.Ascii, detected.Value.Kind);
        Assert.Equal(1.0, detected.Confidence);
    }

    [Fact]
    public void Detect_AsciiPartialSample_IsAsciiWithLowerConfidence()
    {
        var detected = TextEncodingDetector.Detect("a;b\r\n1;2\r\n"u8, isWholeFile: false);

        Assert.Equal(TextEncodingKind.Ascii, detected.Value.Kind);
        Assert.True(detected.Confidence < 1.0);
        Assert.Contains("may still be", detected.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_ValidUtf8Accents_IsUtf8()
    {
        var detected = TextEncodingDetector.Detect(Encoding.UTF8.GetBytes("Città;Perché;Più\r\n"), isWholeFile: true);

        Assert.Equal(new TextEncodingInfo(TextEncodingKind.Utf8, 0), detected.Value);
        Assert.Contains("3 multi-byte", detected.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_Windows1252Accents_IsWindows1252()
    {
        var detected = TextEncodingDetector.Detect(_windows1252.GetBytes("Città;Perché\r\n"), isWholeFile: true);

        Assert.Equal(TextEncodingKind.Windows1252, detected.Value.Kind);
        Assert.Contains("offset 4", detected.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_Utf8SequenceCutByPartialSample_IsStillUtf8()
    {
        var bytes = Encoding.UTF8.GetBytes("Città è");

        var detected = TextEncodingDetector.Detect(bytes.AsSpan(0, bytes.Length - 1), isWholeFile: false);

        Assert.Equal(TextEncodingKind.Utf8, detected.Value.Kind);
    }

    [Fact]
    public void Detect_Utf8SequenceCutAtEndOfFile_IsWindows1252()
    {
        var bytes = Encoding.UTF8.GetBytes("Città è");

        var detected = TextEncodingDetector.Detect(bytes.AsSpan(0, bytes.Length - 1), isWholeFile: true);

        Assert.Equal(TextEncodingKind.Windows1252, detected.Value.Kind);
    }

    [Fact]
    public void FromOptions_MatchingBom_IsSkipped()
    {
        var detected = TextEncodingDetector.FromOptions(TextEncodingKind.Utf8, [0xEF, 0xBB, 0xBF, (byte)'a']);

        Assert.Equal(3, detected.Value.BomLength);
        Assert.Equal(1.0, detected.Confidence);
    }

    [Fact]
    public void FromOptions_BomOfAnotherEncoding_IsText()
    {
        var detected = TextEncodingDetector.FromOptions(TextEncodingKind.Windows1252, [0xEF, 0xBB, 0xBF, (byte)'a']);

        Assert.Equal(0, detected.Value.BomLength);
    }

    [Theory]
    [InlineData(TextEncodingKind.Ascii, "ascii")]
    [InlineData(TextEncodingKind.Utf8, "utf-8")]
    [InlineData(TextEncodingKind.Utf16LittleEndian, "utf-16le")]
    [InlineData(TextEncodingKind.Utf16BigEndian, "utf-16be")]
    [InlineData(TextEncodingKind.Windows1252, "windows-1252")]
    public void Id_EveryKind_MatchesAdr0006(TextEncodingKind kind, string expected)
    {
        Assert.Equal(expected, new TextEncodingInfo(kind, 0).Id);
    }

    [Fact]
    public void Encoding_Windows1252_DecodesEuroSign()
    {
        Assert.Equal("€", _windows1252.GetString([0x80]));
    }
}
