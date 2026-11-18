namespace Fedele.Core;

/// <summary>Reads one file format, in streaming. Implemented by the format modules, such as Fedele.Formats.Csv.</summary>
public interface IFormatReader
{
    /// <summary>The format identifier, such as <c>csv</c>.</summary>
    string FormatId { get; }

    /// <summary>Tells how likely it is that a file starting with <paramref name="sample"/> is in this format.</summary>
    FormatDetection Detect(ReadOnlySpan<byte> sample);

    /// <summary>
    /// Opens a file: reads a sample from the start of <paramref name="input"/> and deduces its layout.
    /// The stream does not need to be seekable. Disposing the reader does not dispose the stream.
    /// </summary>
    ValueTask<IRecordReader> OpenAsync(Stream input, ReadOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>An opened file: its layout, its records in streaming, the problems found and, at the end, a summary.</summary>
public interface IRecordReader : IAsyncDisposable
{
    /// <summary>The layout deduced when the file was opened, each fact with its confidence and reason.</summary>
    FormatLayout Layout { get; }

    /// <summary>The issues found so far, up to <see cref="ReadOptions.MaxIssues"/>.</summary>
    IReadOnlyList<Issue> Issues { get; }

    /// <summary>How many issues were found beyond <see cref="ReadOptions.MaxIssues"/>: counted, not kept.</summary>
    long DroppedIssueCount { get; }

    /// <summary>What is known after reading the whole file, or <see langword="null"/> until reading is finished.</summary>
    ReadSummary? Summary { get; }

    /// <summary>Reads the data records; the header is in <see cref="FormatLayout.ColumnNames"/>. Can be called only once.</summary>
    IAsyncEnumerable<RawRecord> ReadRecordsAsync(CancellationToken cancellationToken = default);
}
