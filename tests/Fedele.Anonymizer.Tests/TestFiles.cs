using System.Text;

namespace Fedele.Anonymizer.Tests;

/// <summary>Builds in-memory test files with exact bytes.</summary>
internal static class TestFiles
{
    // Declared first: static field initializers run in textual order.
    private static readonly Encoding _windows1252 = CreateWindows1252();

    /// <summary>Files covering encodings, BOMs, line endings and malformations.</summary>
    public static readonly Dictionary<string, byte[]> Variants = new()
    {
        ["utf8-bom-crlf"] = Utf8("Codice;Nome;Importo\r\n00123;Müller;1.234,56\r\n", bom: true),
        ["utf8-lf-no-final-newline"] = Utf8("a,b\n1,\"x, y\"\n2,z"),
        ["windows1252-cr"] = Windows1252("Città;Prezzo\r€ 5;perché\r"),
        ["utf16le-bom"] = Utf16("Nome;Età\r\nAnna;30\r\n", bigEndian: false),
        ["utf16be-bom"] = Utf16("Nome;Età\r\nAnna;30\r\n", bigEndian: true),
        ["utf16le-odd-trailing-byte"] = [.. Utf16("a;b\r\nc;d\r\n", bigEndian: false), 0x41],
        ["quoted-multiline"] = Utf8("a;b\r\n\"x;\"\"y\"\"\r\nz\";2\r\n"),
        ["unterminated-quote"] = Utf8("a;b\n\"open;1\n"),
        ["empty-lines-irregular"] = Utf8("a;b;c\n\n1;2\n1;2;3;4\n\n"),
        ["mixed-line-endings"] = Utf8("a;b\r\n1;2\n3;4\r5;6"),
        ["text-after-quote-and-stray-quote"] = Utf8("a;b\n\"x\"y;ab\"c\n"),
        ["dates-and-numbers"] = Utf8("Data;Ora;Importo\n25/03/2020;2021-07-04T10:30:00;-0,50\n"),
        ["empty-file"] = [],
    };

    public static TheoryData<string> VariantNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in Variants.Keys)
        {
            data.Add(name);
        }

        return data;
    }

    public static byte[] Utf8(string text, bool bom = false) =>
        bom ? [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)] : Encoding.UTF8.GetBytes(text);

    public static byte[] Windows1252(string text) => _windows1252.GetBytes(text);

    public static byte[] Utf16(string text, bool bigEndian, bool bom = true)
    {
        var encoding = new UnicodeEncoding(bigEndian, byteOrderMark: true);
        return bom ? [.. encoding.GetPreamble(), .. encoding.GetBytes(text)] : encoding.GetBytes(text);
    }

    public static FileAnalysis Analyze(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return FileAnalyzer.Analyze(stream);
    }

    public static string Report(byte[] bytes)
    {
        using var writer = new StringWriter();
        AnalysisReport.Write(writer, "test.csv", Analyze(bytes));
        return writer.ToString();
    }

    public static byte[] Anonymize(byte[] bytes, int[] columns, int seed = 1, bool hasHeader = true)
    {
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        FileAnonymizer.Anonymize(input, output, new AnonymizeOptions(columns.ToHashSet(), seed, hasHeader));
        return output.ToArray();
    }

    private static Encoding CreateWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
