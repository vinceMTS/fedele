using Fedele.Tests.Shared;

namespace Fedele.Core.Tests;

public sealed class FedeleEngineTests
{
    [Fact]
    public void AddFormat_SameIdTwice_Throws()
    {
        var builder = new FedeleBuilder().AddFormat(new FakeFormat("csv", 0.5));

        Assert.Throws<InvalidOperationException>(() => builder.AddFormat(new FakeFormat("CSV", 0.5)));
    }

    [Fact]
    public void FindFormat_DifferentCase_FindsIt()
    {
        var engine = new FedeleBuilder().AddFormat(new FakeFormat("csv", 0.5)).Build();

        Assert.NotNull(engine.FindFormat("CSV"));
        Assert.Null(engine.FindFormat("json"));
    }

    [Fact]
    public void Detect_SeveralFormats_ReturnsMostConfident()
    {
        var engine = new FedeleBuilder()
            .AddFormat(new FakeFormat("a", 0.3))
            .AddFormat(new FakeFormat("b", 0.8))
            .AddFormat(new FakeFormat("c", 0.5))
            .Build();

        Assert.Equal("b", engine.Detect([1, 2, 3])?.FormatId);
    }

    [Fact]
    public void Detect_NoFormatRecognizesTheBytes_ReturnsNull()
    {
        var engine = new FedeleBuilder().AddFormat(new FakeFormat("a", 0)).Build();

        Assert.Null(engine.Detect([1, 2, 3]));
    }

    [Fact]
    public async Task OpenAsync_NonSeekableStream_FormatReadsFromTheFirstByte()
    {
        var bytes = Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251)).ToArray();
        var format = new FakeFormat("a", 0.9);
        var engine = new FedeleBuilder().AddFormat(format).Build();

        await using var stream = new NonSeekableStream(bytes, maxChunk: 7_000);
        await using var reader = await engine.OpenAsync(stream, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(bytes, format.LastRead);
    }

    [Fact]
    public async Task OpenAsync_UnrecognizedBytes_ThrowsNotSupported()
    {
        var engine = new FedeleBuilder().AddFormat(new FakeFormat("a", 0)).Build();
        await using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await engine.OpenAsync(stream, cancellationToken: TestContext.Current.CancellationToken));
    }

    private sealed class FakeFormat(string id, double confidence) : IFormatReader
    {
        public string FormatId => id;

        public byte[]? LastRead { get; private set; }

        public FormatDetection Detect(ReadOnlySpan<byte> sample) => new(id, confidence, "fake");

        public async ValueTask<IRecordReader> OpenAsync(Stream input, ReadOptions? options = null, CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream();
            await input.CopyToAsync(copy, cancellationToken);
            LastRead = copy.ToArray();
            return new FakeReader();
        }
    }

    private sealed class FakeReader : IRecordReader
    {
        public FormatLayout Layout => throw new NotSupportedException();

        public IReadOnlyList<Issue> Issues => [];

        public long DroppedIssueCount => 0;

        public ReadSummary? Summary => null;

        public IAsyncEnumerable<RawRecord> ReadRecordsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
