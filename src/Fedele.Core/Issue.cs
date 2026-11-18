namespace Fedele.Core;

/// <summary>How serious a problem is.</summary>
public enum IssueSeverity
{
    /// <summary>Worth knowing; the data is read as it is.</summary>
    Info,

    /// <summary>The data is read, but probably not as its author meant.</summary>
    Warning,

    /// <summary>The data cannot be read faithfully at this point.</summary>
    Error,
}

/// <summary>Where a problem was found.</summary>
/// <param name="Record">Record number, counting the header as 1; blank lines are not counted.</param>
/// <param name="Line">Physical line number in the file, starting from 1.</param>
/// <param name="Column">Column number, starting from 1, or <see langword="null"/> when the problem concerns the whole record.</param>
/// <param name="ColumnName">Column name from the header, when there is one.</param>
public sealed record IssueLocation(long Record, long Line, int? Column = null, string? ColumnName = null);

/// <summary>A problem Fedele found while reading. Fedele never fixes data silently: it reports instead.</summary>
/// <param name="Code">Stable identifier of the kind of problem, such as <c>empty-record</c>. Codes are added, never renamed.</param>
/// <param name="Severity">How serious the problem is.</param>
/// <param name="Message">Explanation for a person, in English.</param>
/// <param name="Location">Where the problem is, or <see langword="null"/> when it concerns the whole file.</param>
public sealed record Issue(string Code, IssueSeverity Severity, string Message, IssueLocation? Location = null);

/// <summary>Issue codes shared by every format.</summary>
public static class IssueCodes
{
    /// <summary>Bytes that are not valid in the file's encoding; the text holds U+FFFD in their place.</summary>
    public const string InvalidByte = "invalid-byte";
}

/// <summary>
/// Collects issues up to a limit, so that a file with millions of problems does not fill the memory.
/// Issues beyond the limit are counted, not kept.
/// </summary>
public sealed class IssueCollector
{
    private readonly List<Issue> _issues = [];

    /// <summary>Creates a collector that keeps at most <paramref name="limit"/> issues.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is negative.</exception>
    public IssueCollector(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        Limit = limit;
    }

    /// <summary>The maximum number of issues kept.</summary>
    public int Limit { get; }

    /// <summary>The issues kept, in the order they were found.</summary>
    public IReadOnlyList<Issue> Issues => _issues;

    /// <summary>How many issues were found beyond the limit.</summary>
    public long DroppedCount { get; private set; }

    /// <summary>Adds an issue, or counts it when the limit is reached.</summary>
    public void Add(Issue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (_issues.Count < Limit)
        {
            _issues.Add(issue);
        }
        else
        {
            DroppedCount++;
        }
    }
}
