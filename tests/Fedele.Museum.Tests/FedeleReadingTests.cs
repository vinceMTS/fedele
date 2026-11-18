using Fedele.Core;
using Fedele.Formats.Csv;

namespace Fedele.Museum.Tests;

/// <summary>
/// Reads every case of museum/ with Fedele and compares what Fedele finds with expected.json:
/// the whole <c>file</c> section and the issues. Column types will follow with type inference.
/// </summary>
public sealed class FedeleReadingTests
{
    public static TheoryData<string> Cases => MuseumCases.TheoryData();

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Fedele_FileSection_MatchesExpected(string caseName)
    {
        var expected = MuseumCases.Expected(caseName);
        var result = await ReadAsync(caseName, expected);
        var layout = result.Layout;
        var summary = result.Summary;

        Assert.Equal(expected.File.Encoding, summary.Encoding.Id);
        Assert.Equal(expected.File.Bom, summary.Encoding.HasBom);
        Assert.Equal(expected.File.LineEnding, summary.LineEndingId);
        Assert.Equal(expected.File.FinalLineEnding, summary.FinalLineEnding);
        Assert.Equal(expected.File.Separator, layout.Separator.Value?.ToString());
        Assert.Equal(expected.File.Quote, layout.Quote.ToString());
        Assert.Equal(expected.File.Records, summary.Records);
        Assert.Equal(expected.File.FieldsPerRecord, summary.FieldsPerRecord);
        if (expected.File.Header)
        {
            Assert.Equal(expected.Columns.Select(column => column.Name), layout.ColumnNames);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Fedele_Issues_MatchExpected(string caseName)
    {
        var expected = MuseumCases.Expected(caseName);
        var result = await ReadAsync(caseName, expected);

        Assert.Equal(0, result.DroppedIssues);
        Assert.Equal(
            expected.Issues.Select(issue => (issue.Code, issue.Record, issue.Column)),
            result.Issues.Select(issue => (issue.Code, issue.Location?.Record ?? 0, issue.Location?.ColumnName)));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Fedele_Separator_IsDeducedWithHighConfidence(string caseName)
    {
        var expected = MuseumCases.Expected(caseName);
        var result = await ReadAsync(caseName, expected);

        Assert.True(result.Layout.Separator.Confidence >= 0.9, result.Layout.Separator.Reason);
    }

    private static async Task<MuseumReading> ReadAsync(string caseName, ExpectedCase expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new CsvReadOptions { Header = expected.File.Header ? HeaderMode.Present : HeaderMode.Absent };
        await using var stream = File.OpenRead(MuseumCases.InputPath(caseName));
        await using var reader = await new CsvFormatReader().OpenCsvAsync(stream, options, cancellationToken);
        await foreach (var _ in reader.ReadRecordsAsync(cancellationToken))
        {
        }

        return new MuseumReading(
            (CsvLayout)reader.Layout,
            Assert.IsType<CsvReadSummary>(reader.Summary),
            reader.Issues,
            reader.DroppedIssueCount);
    }

    private sealed record MuseumReading(CsvLayout Layout, CsvReadSummary Summary, IReadOnlyList<Issue> Issues, long DroppedIssues);
}
