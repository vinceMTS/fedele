using System.Globalization;
using Fedele.Core;

namespace Fedele.Formats.Csv;

/// <summary>Issue codes of the CSV format. Codes are added, never renamed.</summary>
public static class CsvIssueCodes
{
    /// <summary>A record whose fields are all empty, such as <c>,,,,</c>.</summary>
    public const string EmptyRecord = "empty-record";

    /// <summary>A record with a different number of fields than the header, or than the first record.</summary>
    public const string FieldCountMismatch = "field-count-mismatch";

    /// <summary>A quoted field never closed: the rest of the file was read as part of it.</summary>
    public const string UnterminatedQuote = "unterminated-quote";

    /// <summary>Text after the closing quote of a field, as in <c>"abc"def</c>.</summary>
    public const string TextAfterClosingQuote = "text-after-closing-quote";

    /// <summary>A quote in the middle of an unquoted field, as in <c>ab"c</c>.</summary>
    public const string StrayQuote = "stray-quote";

    /// <summary>Two separators split the sample equally well.</summary>
    public const string AmbiguousSeparator = "ambiguous-separator";
}

/// <summary>Options for reading a CSV file. Every option left unset is deduced by Fedele.</summary>
public sealed record CsvReadOptions : ReadOptions
{
    /// <summary>The field separator, or <see langword="null"/> to deduce it.</summary>
    public char? Separator { get; init; }

    /// <summary>The quote character. Default: <c>"</c>.</summary>
    public char Quote { get; init; } = '"';

    /// <summary>Returns CSV options from generic ones, keeping every value they set.</summary>
    public static CsvReadOptions From(ReadOptions? options) => options switch
    {
        CsvReadOptions csv => csv,
        null => new CsvReadOptions(),
        _ => new CsvReadOptions { Header = options.Header, Encoding = options.Encoding, MaxIssues = options.MaxIssues },
    };
}

/// <summary>How the lines of a file end.</summary>
public enum LineEndingStyle
{
    /// <summary>The file has a single line, with no line break.</summary>
    None,

    /// <summary>CR LF, the Windows convention.</summary>
    CrLf,

    /// <summary>LF, the Unix convention.</summary>
    Lf,

    /// <summary>CR alone, the old Mac convention.</summary>
    Cr,

    /// <summary>More than one convention in the same file.</summary>
    Mixed,
}

/// <summary>The layout of a CSV file, as deduced when it is opened.</summary>
/// <param name="Encoding">The text encoding.</param>
/// <param name="Separator">The field separator, or <see langword="null"/> for a single-column file.</param>
/// <param name="Quote">The quote character.</param>
/// <param name="Header">Whether the first record holds the column names.</param>
/// <param name="ColumnNames">The column names, exactly as written in the header; <see langword="null"/> without a header.</param>
public sealed record CsvLayout(
    Detected<TextEncodingInfo> Encoding,
    Detected<char?> Separator,
    char Quote,
    HeaderMode Header,
    IReadOnlyList<string>? ColumnNames) : FormatLayout(CsvFormatReader.Id, ColumnNames)
{
    /// <inheritdoc/>
    public override IReadOnlyList<LayoutFact> Describe() =>
    [
        new("encoding", Encoding.Value.Id, Encoding.Confidence, Encoding.Reason),
        new("bom", Encoding.Value.HasBom ? "yes" : "no"),
        new("separator", DisplaySeparator(Separator.Value), Separator.Confidence, Separator.Reason),
        new("quote", Quote.ToString()),
        new("header", Header == HeaderMode.Present
            ? string.Create(CultureInfo.InvariantCulture, $"yes, {ColumnNames?.Count ?? 0} columns")
            : "no", null, "Set in the read options (header detection comes with type inference)."),
    ];

    /// <summary>Shows a separator to a person: <c>';'</c>, <c>TAB</c>, or <c>none</c>.</summary>
    public static string DisplaySeparator(char? separator) => separator switch
    {
        null => "none",
        '\t' => "TAB",
        var c => $"'{c}'",
    };
}

/// <summary>What is known about a CSV file after reading all of it.</summary>
/// <param name="Records">Data records, header excluded, blank lines excluded.</param>
/// <param name="Encoding">The encoding, confirmed by the whole file: a file deduced as ASCII becomes UTF-8 if later bytes are UTF-8.</param>
/// <param name="LineEnding">How records end.</param>
/// <param name="FinalLineEnding">True when the file ends with a line break.</param>
/// <param name="FieldsPerRecord">The most common number of fields per record, header included; 0 for an empty file.</param>
/// <param name="BlankLines">Completely empty lines, skipped.</param>
/// <param name="CrLfCount">Records ended by CR LF.</param>
/// <param name="LfCount">Records ended by LF alone.</param>
/// <param name="CrCount">Records ended by CR alone.</param>
public sealed record CsvReadSummary(
    long Records,
    TextEncodingInfo Encoding,
    LineEndingStyle LineEnding,
    bool FinalLineEnding,
    int FieldsPerRecord,
    long BlankLines,
    long CrLfCount,
    long LfCount,
    long CrCount) : ReadSummary(Records)
{
    /// <summary>Stable identifier of the line ending, as in ADR 0006: <c>crlf</c>, <c>lf</c>, <c>cr</c>, <c>mixed</c> or <c>none</c>.</summary>
    public string LineEndingId => LineEnding switch
    {
        LineEndingStyle.CrLf => "crlf",
        LineEndingStyle.Lf => "lf",
        LineEndingStyle.Cr => "cr",
        LineEndingStyle.Mixed => "mixed",
        _ => "none",
    };

    /// <inheritdoc/>
    public override IReadOnlyList<LayoutFact> Describe() =>
    [
        new("records", Records.ToString(CultureInfo.InvariantCulture)),
        new("encoding", Encoding.Id),
        new("line ending", LineEndingId, null, string.Create(
            CultureInfo.InvariantCulture,
            $"CR LF: {CrLfCount}, LF: {LfCount}, CR: {CrCount}.")),
        new("final line ending", FinalLineEnding ? "yes" : "no"),
        new("fields per record", FieldsPerRecord.ToString(CultureInfo.InvariantCulture)),
        new("blank lines", BlankLines.ToString(CultureInfo.InvariantCulture)),
    ];
}
