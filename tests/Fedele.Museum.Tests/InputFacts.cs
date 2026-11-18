using System.Text;

namespace Fedele.Museum.Tests;

/// <summary>
/// Facts read directly from the bytes of an input file, using the separator and quote that
/// expected.json declares. It is a cross-check of expected.json, not a CSV reader: types and
/// issues are left to Fedele itself.
/// </summary>
internal sealed record InputFacts(
    long CrLf,
    long Lf,
    long Cr,
    bool FinalLineEnding,
    long Records,
    IReadOnlyList<string> FirstRecord)
{
    private static readonly Encoding _windows1252 = CreateWindows1252();

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static ReadOnlySpan<byte> Utf16LittleEndianBom => [0xFF, 0xFE];

    private static ReadOnlySpan<byte> Utf16BigEndianBom => [0xFE, 0xFF];

    /// <summary>The line ending name of ADR 0006, or null when the file has none.</summary>
    public string? LineEnding
    {
        get
        {
            (string Name, long Count)[] all = [("crlf", CrLf), ("lf", Lf), ("cr", Cr)];
            var present = all.Where(kind => kind.Count > 0).ToList();
            return present.Count switch
            {
                0 => null,
                1 => present[0].Name,
                _ => "mixed",
            };
        }
    }

    /// <summary>The length of the BOM that the bytes start with for the given encoding name, or 0.</summary>
    public static int BomLength(ReadOnlySpan<byte> bytes, string encoding) => encoding switch
    {
        "utf-8" or "ascii" when bytes.StartsWith(Utf8Bom) => Utf8Bom.Length,
        "utf-16le" when bytes.StartsWith(Utf16LittleEndianBom) => Utf16LittleEndianBom.Length,
        "utf-16be" when bytes.StartsWith(Utf16BigEndianBom) => Utf16BigEndianBom.Length,
        _ => 0,
    };

    /// <summary>Decodes the bytes after the BOM, throwing on any byte that is invalid for the encoding.</summary>
    public static string Decode(byte[] bytes, string encoding, int bomLength)
    {
        var strict = encoding switch
        {
            "ascii" => Encoding.GetEncoding("us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
            "utf-8" => new UTF8Encoding(false, throwOnInvalidBytes: true),
            "utf-16le" => new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true),
            "utf-16be" => new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true),
            "windows-1252" => _windows1252,
            _ => throw new ArgumentException($"Unknown encoding: {encoding}", nameof(encoding)),
        };
        return strict.GetString(bytes, bomLength, bytes.Length - bomLength);
    }

    /// <summary>True when the bytes are valid UTF-8 (so a file declared windows-1252 would be mislabeled).</summary>
    public static bool IsValidUtf8(byte[] bytes) => System.Text.Unicode.Utf8.IsValid(bytes);

    /// <summary>Scans records quote-aware: line breaks inside quotes do not end a record.</summary>
    public static InputFacts Scan(string text, char separator, char quote)
    {
        long crLf = 0, lf = 0, cr = 0, records = 0;
        List<string>? firstRecord = null;
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var recordHasContent = false;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRecord()
        {
            EndField();
            if (recordHasContent)
            {
                records++;
                firstRecord ??= [.. fields];
            }

            fields.Clear();
            recordHasContent = false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c != quote)
                {
                    field.Append(c);
                }
                else if (i + 1 < text.Length && text[i + 1] == quote)
                {
                    field.Append(quote);
                    i++;
                }
                else
                {
                    inQuotes = false;
                }

                continue;
            }

            if (c == quote)
            {
                inQuotes = true;
                recordHasContent = true;
            }
            else if (c == separator)
            {
                EndField();
                recordHasContent = true;
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    crLf++;
                    i++;
                }
                else if (c == '\r')
                {
                    cr++;
                }
                else
                {
                    lf++;
                }

                EndRecord();
            }
            else
            {
                field.Append(c);
                recordHasContent = true;
            }
        }

        var finalLineEnding = !inQuotes && text.Length > 0 && text[^1] is '\r' or '\n';
        if (recordHasContent)
        {
            EndRecord();
        }

        return new InputFacts(crLf, lf, cr, finalLineEnding, records, firstRecord ?? []);
    }

    private static Encoding CreateWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
