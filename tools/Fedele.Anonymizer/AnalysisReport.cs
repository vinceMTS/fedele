using System.Globalization;
using System.Text;

namespace Fedele.Anonymizer;

/// <summary>
/// Writes a <see cref="FileAnalysis"/> as text. It can only print what the analysis holds:
/// counts, shapes and header names, never values.
/// </summary>
internal static class AnalysisReport
{
    private const int MaxNameLength = 40;

    public static void Write(TextWriter writer, string fileName, FileAnalysis analysis)
    {
        writer.WriteLine(fileName);
        writer.WriteLine($"  Size           : {analysis.SizeBytes} bytes");
        writer.WriteLine($"  Encoding       : {analysis.Encoding.DisplayName}; BOM: {analysis.Encoding.BomDisplay}");
        writer.WriteLine($"  Line endings   : CRLF {analysis.CrLf}, LF {analysis.Lf}, CR {analysis.Cr}; last line terminated: {YesNo(analysis.EndsWithLineEnding)}");
        writer.WriteLine($"  Separator      : {analysis.Separator.Display}{SeparatorConfidence(analysis.Separator)}");
        writer.WriteLine($"  Quoting        : {analysis.QuotedFields} quoted fields, {analysis.FieldsWithLineBreaks} with line breaks inside, "
            + $"{analysis.UnterminatedQuotes} unterminated, {analysis.TextAfterClosingQuote} with text after the closing quote, "
            + $"{analysis.StrayQuotes} stray quotes");
        writer.WriteLine($"  Records        : {analysis.Records} (empty lines not counted: {analysis.EmptyRecords})");
        writer.WriteLine($"  Fields/record  : {FieldCounts(analysis)}");
        writer.WriteLine(analysis.HeaderNames is null
            ? "  Header         : not detected - the first record does not look like column names; names hidden, first record counted as data"
            : "  Header         : yes, the first record looks like column names");
        writer.WriteLine("  Columns        :");

        for (var i = 0; i < analysis.Columns.Count || i < (analysis.HeaderNames?.Count ?? 0); i++)
        {
            var name = analysis.HeaderNames is { } names && i < names.Count ? Sanitize(names[i]) : $"#{i + 1}";
            var stats = i < analysis.Columns.Count ? analysis.Columns[i] : null;
            writer.WriteLine($"    {i + 1,3}. {name,-MaxNameLength} {Describe(stats)}");
        }
    }

    private static string SeparatorConfidence(SeparatorGuess separator) =>
        separator.Separator is null
            ? string.Empty
            : $" (same field count in {Percent(separator.Consistency)} of {separator.SampledRecords} sampled records)";

    private static string FieldCounts(FileAnalysis analysis)
    {
        if (analysis.FieldCounts.Count == 0)
        {
            return "no records";
        }

        var counts = string.Join("; ", analysis.FieldCounts
            .OrderByDescending(pair => pair.Value)
            .Select(pair => $"{pair.Key} fields in {pair.Value} records"));
        return analysis.IrregularRecords.Count == 0
            ? counts
            : $"{counts} (first irregular records: {string.Join(", ", analysis.IrregularRecords.Select(n => $"#{n}"))})";
    }

    private static string Describe(ColumnStats? stats)
    {
        if (stats is null || stats.Values == 0)
        {
            return "no data";
        }

        var parts = new List<string>
        {
            string.Join(", ", stats.Kinds
                .OrderByDescending(pair => pair.Value)
                .Select(pair => $"{ValueClassifier.Describe(pair.Key)} {Percent((double)pair.Value / stats.Values)}")),
        };

        if (stats.DatePatterns.Count > 0)
        {
            parts.Add("patterns " + string.Join(", ", stats.DatePatterns
                .OrderByDescending(pair => pair.Value)
                .Take(3)
                .Select(pair => $"{pair.Key} ({pair.Value})")));
            parts.Add($"day first proven by {stats.DayFirstProofs}, month first by {stats.MonthFirstProofs}");
        }

        AddCount(parts, "leading zeros", stats.LeadingZeros);
        AddCount(parts, "surrounding spaces", stats.SurroundingWhitespace);
        AddCount(parts, "quoted", stats.Quoted);
        AddCount(parts, "line breaks inside", stats.LineBreaks);
        parts.Add($"max length {stats.MaxLength}");
        return string.Join(" | ", parts);
    }

    private static void AddCount(List<string> parts, string label, long count)
    {
        if (count > 0)
        {
            parts.Add($"{label} {count}");
        }
    }

    private static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(char.IsControl(c) ? '?' : c);
        }

        return builder.Length <= MaxNameLength ? builder.ToString() : builder.ToString(0, MaxNameLength - 3) + "...";
    }

    private static string Percent(double fraction) =>
        (fraction * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static string YesNo(bool value) => value ? "yes" : "no";
}
