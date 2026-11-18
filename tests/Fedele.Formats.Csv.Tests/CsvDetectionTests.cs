using Fedele.Core;
using Fedele.Tests.Shared;

namespace Fedele.Formats.Csv.Tests;

public sealed class CsvDetectionTests
{
    [Fact]
    public void Detect_ConsistentCsv_IsVeryLikely()
    {
        var detection = new CsvFormatReader().Detect("a;b;c\r\n1;2;3\r\n4;5;6\r\n"u8);

        Assert.Equal("csv", detection.FormatId);
        Assert.True(detection.Confidence > 0.9);
        Assert.Contains("';'", detection.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Detect_BinaryBytes_IsNotCsv()
    {
        var detection = new CsvFormatReader().Detect([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x08, 0x00]);

        Assert.Equal(0, detection.Confidence);
    }

    [Fact]
    public void Detect_TextWithoutSeparator_IsUnlikely()
    {
        var detection = new CsvFormatReader().Detect("just some words\nand more words\n"u8);

        Assert.Equal(0.3, detection.Confidence);
    }

    [Fact]
    public async Task FedeleEngine_WithCsv_OpensNonSeekableStreamByDetection()
    {
        var engine = new FedeleBuilder().AddCsv().Build();
        await using var stream = new NonSeekableStream("a;b\r\n1;2\r\n"u8.ToArray(), maxChunk: 3);

        await using var reader = await engine.OpenAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        var records = new List<RawRecord>();
        await foreach (var record in reader.ReadRecordsAsync(TestContext.Current.CancellationToken))
        {
            records.Add(record);
        }

        Assert.Equal("csv", reader.Layout.FormatId);
        Assert.Equal(["a", "b"], reader.Layout.ColumnNames);
        Assert.Equal(["1", "2"], records.Single().Fields.Select(field => field.Text));
    }

    [Fact]
    public void Describe_Layout_ListsEveryFactWithReasons()
    {
        var layout = new CsvLayout(
            new Detected<TextEncodingInfo>(new TextEncodingInfo(TextEncodingKind.Utf8, 3), 1, "BOM."),
            new Detected<char?>('\t', 0.9, "Tabs."),
            '"',
            HeaderMode.Present,
            ["a", "b"]);

        var facts = layout.Describe();

        Assert.Equal(["encoding", "bom", "separator", "quote", "header"], facts.Select(fact => fact.Name));
        Assert.Equal(["utf-8", "yes", "TAB", "\"", "yes, 2 columns"], facts.Select(fact => fact.Value));
    }
}
