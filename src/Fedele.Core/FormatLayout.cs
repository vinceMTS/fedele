namespace Fedele.Core;

/// <summary>One fact about a file, ready to be shown to a person.</summary>
/// <param name="Name">What the fact is about, such as <c>separator</c>.</param>
/// <param name="Value">The value, as text.</param>
/// <param name="Confidence">How sure Fedele is, from 0 to 1; <see langword="null"/> for facts that were measured or set, not deduced.</param>
/// <param name="Reason">Why, when the fact was deduced or set.</param>
public sealed record LayoutFact(string Name, string Value, double? Confidence = null, string? Reason = null);

/// <summary>
/// How a file is laid out, as deduced when it is opened. Each format extends it with its own facts.
/// </summary>
/// <param name="FormatId">The format, such as <c>csv</c>.</param>
/// <param name="ColumnNames">The column names, or <see langword="null"/> when the file has no header.</param>
public abstract record FormatLayout(string FormatId, IReadOnlyList<string>? ColumnNames)
{
    /// <summary>Describes the layout as a list of facts, for display.</summary>
    public abstract IReadOnlyList<LayoutFact> Describe();
}

/// <summary>
/// What is known about a file only after reading all of it. Each format extends it with its own facts.
/// </summary>
/// <param name="Records">Data records read, header excluded, blank lines excluded.</param>
public abstract record ReadSummary(long Records)
{
    /// <summary>Describes the summary as a list of facts, for display.</summary>
    public abstract IReadOnlyList<LayoutFact> Describe();
}

/// <summary>How likely it is that some bytes are in a given format.</summary>
/// <param name="FormatId">The format, such as <c>csv</c>.</param>
/// <param name="Confidence">From 0 (certainly not) to 1 (certainly).</param>
/// <param name="Reason">Why, in a sentence a person can read.</param>
public sealed record FormatDetection(string FormatId, double Confidence, string Reason);
