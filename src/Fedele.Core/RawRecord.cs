namespace Fedele.Core;

/// <summary>One field of a record, as text exactly as it is in the file: quotes removed, nothing else changed.</summary>
/// <param name="Text">The field's text. Escaped quotes are already unescaped; spaces are kept.</param>
/// <param name="Quoted">True when the field was enclosed in quotes.</param>
public readonly record struct RawField(string Text, bool Quoted);

/// <summary>A record read from a file, before any type is deduced.</summary>
/// <param name="Number">Record number, counting the header as 1; blank lines are not counted.</param>
/// <param name="Line">Physical line where the record starts, from 1.</param>
/// <param name="ByteOffset">Position in bytes where the record starts, byte order mark included.</param>
/// <param name="Fields">The fields of the record.</param>
public sealed record RawRecord(long Number, long Line, long ByteOffset, IReadOnlyList<RawField> Fields);
