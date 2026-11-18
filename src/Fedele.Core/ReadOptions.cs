namespace Fedele.Core;

/// <summary>Whether the first record of a file holds the column names.</summary>
public enum HeaderMode
{
    /// <summary>The first record holds the column names.</summary>
    Present,

    /// <summary>Every record is data.</summary>
    Absent,
}

/// <summary>Options for reading a file. Every option left unset is deduced by Fedele.</summary>
public record ReadOptions
{
    /// <summary>Whether the first record holds the column names. Default: <see cref="HeaderMode.Present"/>.</summary>
    public HeaderMode Header { get; init; } = HeaderMode.Present;

    /// <summary>The text encoding, or <see langword="null"/> to deduce it.</summary>
    public TextEncodingKind? Encoding { get; init; }

    /// <summary>The maximum number of issues kept in memory; the rest are only counted. Default: 1000.</summary>
    public int MaxIssues { get; init; } = 1000;
}
