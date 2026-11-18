using System.Text;
using Fedele.Core;
using Fedele.Tests.Shared;

namespace Fedele.Formats.Csv.Tests;

/// <summary>Reads a whole CSV from bytes, through a non-seekable stream, as a real upload would arrive.</summary>
internal sealed record CsvTestResult(CsvLayout Layout, IReadOnlyList<RawRecord> Records, IReadOnlyList<Issue> Issues, long DroppedIssues, CsvReadSummary Summary)
{
    public IEnumerable<string[]> Texts => Records.Select(record => record.Fields.Select(f => f.Text).ToArray());

    public IEnumerable<(string Code, long Record)> IssueKeys => Issues.Select(issue => (issue.Code, issue.Location?.Record ?? 0));
}

internal static class CsvTestReader
{
    public static Task<CsvTestResult> ReadAsync(string text, CsvReadOptions? options = null, int maxChunk = int.MaxValue) =>
        ReadAsync(Encoding.UTF8.GetBytes(text), options, maxChunk);

    public static async Task<CsvTestResult> ReadAsync(byte[] bytes, CsvReadOptions? options = null, int maxChunk = int.MaxValue)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var stream = new NonSeekableStream(bytes, maxChunk);
        await using var reader = await new CsvFormatReader().OpenCsvAsync(stream, options, cancellationToken);

        var records = new List<RawRecord>();
        await foreach (var record in reader.ReadRecordsAsync(cancellationToken))
        {
            records.Add(record);
        }

        return new CsvTestResult(
            (CsvLayout)reader.Layout,
            records,
            reader.Issues,
            reader.DroppedIssueCount,
            Assert.IsType<CsvReadSummary>(reader.Summary));
    }
}
