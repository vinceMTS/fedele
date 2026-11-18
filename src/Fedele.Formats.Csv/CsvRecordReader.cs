using System.Globalization;
using System.Runtime.CompilerServices;
using Fedele.Core;

namespace Fedele.Formats.Csv;

/// <summary>
/// An opened CSV file. It reads the stream in chunks, feeds the code units to <see cref="CsvLexer"/>
/// and assembles the records, keeping track of lines, byte offsets and problems.
/// </summary>
internal sealed class CsvRecordReader : IRecordReader, ICsvSink
{
    private const int ChunkSize = 64 * 1024;
    private const int Cr = '\r';
    private const int Lf = '\n';

    private readonly Stream _input;
    private readonly TextEncodingInfo _encoding;
    private readonly HeaderMode _headerMode;
    private readonly CsvLexer _lexer;
    private readonly FieldDecoder _field;
    private readonly IssueCollector _issues;
    private readonly Queue<RawRecord> _pending = new();
    private readonly List<RawField> _fields = [];
    private readonly Dictionary<int, long> _fieldCounts = [];
    private readonly byte[] _buffer = new byte[ChunkSize];

    private CsvLayout? _layout;
    private int _carry = -1;
    private bool _started;
    private bool _finished;

    // Position in the file.
    private long _unitIndex;
    private long _line = 1;
    private int _previousUnit = -1;
    private long _recordStartUnit;
    private long _recordStartLine = 1;

    // State of the record being read.
    private bool _recordHasContent;
    private int _strayQuoteReportedAt = -1;

    // Facts collected along the way.
    private long _records;
    private long _blankLines;
    private int? _expectedFields;
    private IReadOnlyList<string>? _columnNames;
    private bool _pendingCr;
    private bool _lastUnitWasLineEnd;
    private long _crLf;
    private long _lf;
    private long _cr;

    private CsvRecordReader(Stream input, TextEncodingInfo encoding, char? separator, CsvReadOptions options)
    {
        _input = input;
        _encoding = encoding;
        _headerMode = options.Header;
        _lexer = new CsvLexer(separator ?? CsvLexer.NoSeparator, options.Quote, this);
        _field = new FieldDecoder(encoding);
        _issues = new IssueCollector(options.MaxIssues);
    }

    public FormatLayout Layout => _layout ?? throw new InvalidOperationException("The reader is not open yet.");

    public IReadOnlyList<Issue> Issues => _issues.Issues;

    public long DroppedIssueCount => _issues.DroppedCount;

    public ReadSummary? Summary { get; private set; }

    private long CurrentRecord => _records + 1;

    /// <summary>Reads the sample, deduces the layout and, with a header, reads the header record.</summary>
    public static async ValueTask<CsvRecordReader> OpenAsync(Stream input, CsvReadOptions options, CancellationToken cancellationToken)
    {
        var sample = new byte[CsvFormatReader.SampleSize];
        var length = await input.ReadAtLeastAsync(sample, sample.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        var isWholeFile = length < sample.Length;
        var bytes = sample.AsSpan(0, length);

        var encoding = options.Encoding is { } kind
            ? TextEncodingDetector.FromOptions(kind, bytes)
            : TextEncodingDetector.Detect(bytes, isWholeFile);
        var text = bytes[encoding.Value.BomLength..];

        Detected<char?> separator;
        SeparatorGuess? guess = null;
        if (options.Separator is { } chosen)
        {
            separator = new Detected<char?>(chosen, 1.0, "Set in the read options.");
        }
        else
        {
            guess = SeparatorDetector.Detect(CodeUnits.Read(text, encoding.Value), isWholeFile, options.Quote);
            separator = guess.ToDetected();
        }

        var reader = new CsvRecordReader(input, encoding.Value, separator.Value, options);
        if (guess?.TiedWith is not null)
        {
            reader._issues.Add(new Issue(
                CsvIssueCodes.AmbiguousSeparator,
                IssueSeverity.Warning,
                $"Both {CsvLayout.DisplaySeparator(guess.Separator)} and {CsvLayout.DisplaySeparator(guess.TiedWith)} split the records equally well; "
                + $"{CsvLayout.DisplaySeparator(guess.Separator)} was chosen. Set the separator in the options if it is wrong."));
        }

        reader.Feed(text);
        if (isWholeFile)
        {
            reader.Finish();
        }

        if (options.Header == HeaderMode.Present)
        {
            while (reader._columnNames is null && !reader._finished)
            {
                await reader.FillAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        reader._layout = new CsvLayout(encoding, separator, options.Quote, options.Header, reader._columnNames);
        return reader;
    }

    public IAsyncEnumerable<RawRecord> ReadRecordsAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            throw new InvalidOperationException("The records of a file can be read only once.");
        }

        _started = true;
        return ReadAllAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    void ICsvSink.OnUnit(int unit, UnitRole role)
    {
        // Physical lines, including line breaks inside quoted fields.
        if (unit == Cr || (unit == Lf && _previousUnit != Cr))
        {
            _line++;
        }

        switch (role)
        {
            case UnitRole.Content:
                _field.Append(unit);
                _recordHasContent = true;
                break;
            case UnitRole.Quote:
                _recordHasContent = true;
                break;
            case UnitRole.LineEnd:
                CountLineEnd(unit);
                break;
        }

        if (role != UnitRole.LineEnd && _pendingCr)
        {
            _cr++;
            _pendingCr = false;
        }

        _lastUnitWasLineEnd = role == UnitRole.LineEnd;
        _previousUnit = unit;
        _unitIndex++;
        if (role == UnitRole.LineEnd)
        {
            _recordStartUnit = _unitIndex;
            _recordStartLine = _line;
        }
    }

    void ICsvSink.OnFieldEnd(bool quoted)
    {
        var text = _field.TakeText(out var valid);
        if (!valid)
        {
            Report(
                IssueCodes.InvalidByte,
                IssueSeverity.Warning,
                $"The field holds bytes that are not valid {_encoding.Id}; they appear as U+FFFD in the text. The original bytes are at offset {RecordByteOffset} onwards.",
                _fields.Count + 1);
        }

        _fields.Add(new RawField(text, quoted));
    }

    void ICsvSink.OnRecordEnd()
    {
        if (_fields.Count == 1 && !_recordHasContent)
        {
            _blankLines++;
            ResetRecord();
            return;
        }

        var record = new RawRecord(CurrentRecord, _recordStartLine, RecordByteOffset, [.. _fields]);
        _records++;
        _fieldCounts[_fields.Count] = _fieldCounts.GetValueOrDefault(_fields.Count) + 1;

        if (_records == 1 && _headerMode == HeaderMode.Present)
        {
            _columnNames = [.. _fields.Select(field => field.Text)];
            _expectedFields = _fields.Count;
            ResetRecord();
            return;
        }

        _expectedFields ??= _fields.Count;
        if (_fields.Count != _expectedFields)
        {
            var expectedFrom = _columnNames is null ? "the first record" : "the header";
            Report(
                CsvIssueCodes.FieldCountMismatch,
                IssueSeverity.Warning,
                string.Create(CultureInfo.InvariantCulture, $"The record has {_fields.Count} fields; {expectedFrom} has {_expectedFields}."),
                column: null,
                record: record.Number,
                line: record.Line);
        }

        if (_fields.TrueForAll(field => field.Text.Length == 0))
        {
            Report(
                CsvIssueCodes.EmptyRecord,
                IssueSeverity.Warning,
                "Every field of the record is empty.",
                column: null,
                record: record.Number,
                line: record.Line);
        }

        _pending.Enqueue(record);
        ResetRecord();
    }

    void ICsvSink.OnProblem(CsvProblem problem)
    {
        var column = _fields.Count + 1;
        switch (problem)
        {
            case CsvProblem.UnterminatedQuote:
                Report(
                    CsvIssueCodes.UnterminatedQuote,
                    IssueSeverity.Error,
                    string.Create(CultureInfo.InvariantCulture, $"A quoted field opened on line {_recordStartLine} is never closed: the rest of the file was read as part of it."),
                    column);
                break;
            case CsvProblem.TextAfterClosingQuote:
                Report(
                    CsvIssueCodes.TextAfterClosingQuote,
                    IssueSeverity.Warning,
                    "Text follows the closing quote of the field; it was kept as part of the field.",
                    column);
                break;
            case CsvProblem.StrayQuote when _strayQuoteReportedAt != column:
                _strayQuoteReportedAt = column;
                Report(
                    CsvIssueCodes.StrayQuote,
                    IssueSeverity.Warning,
                    "A quote appears inside a field that is not quoted; it was kept as text.",
                    column);
                break;
        }
    }

    private long RecordByteOffset => _encoding.BomLength + (_recordStartUnit * _encoding.CodeUnitSize);

    private async IAsyncEnumerable<RawRecord> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            while (_pending.TryDequeue(out var record))
            {
                yield return record;
            }

            if (_finished)
            {
                break;
            }

            await FillAsync(cancellationToken).ConfigureAwait(false);
        }

        Summary = BuildSummary();
    }

    private async ValueTask FillAsync(CancellationToken cancellationToken)
    {
        var read = await _input.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            Finish();
            return;
        }

        Feed(_buffer.AsSpan(0, read));
    }

    private void Feed(ReadOnlySpan<byte> bytes)
    {
        if (_encoding.CodeUnitSize == 1)
        {
            foreach (var b in bytes)
            {
                _lexer.Feed(b);
            }

            return;
        }

        var i = 0;
        if (_carry >= 0 && bytes.Length > 0)
        {
            _lexer.Feed(CodeUnits.Combine((byte)_carry, bytes[0], _encoding));
            _carry = -1;
            i = 1;
        }

        for (; i + 1 < bytes.Length; i += 2)
        {
            _lexer.Feed(CodeUnits.Combine(bytes[i], bytes[i + 1], _encoding));
        }

        if (i < bytes.Length)
        {
            _carry = bytes[i];
        }
    }

    private void Finish()
    {
        _lexer.Complete();
        if (_pendingCr)
        {
            _cr++;
            _pendingCr = false;
        }

        if (_carry >= 0)
        {
            _issues.Add(new Issue(
                IssueCodes.InvalidByte,
                IssueSeverity.Warning,
                "The file ends with a single byte that is not a whole UTF-16 code unit; it was ignored."));
            _carry = -1;
        }

        _finished = true;
    }

    private void CountLineEnd(int unit)
    {
        if (unit == Lf)
        {
            if (_pendingCr)
            {
                _crLf++;
                _pendingCr = false;
            }
            else
            {
                _lf++;
            }

            return;
        }

        if (_pendingCr)
        {
            _cr++;
        }

        _pendingCr = true;
    }

    private CsvReadSummary BuildSummary()
    {
        var encoding = _encoding.Kind == TextEncodingKind.Ascii && _field.SawNonAscii
            ? _encoding with { Kind = TextEncodingKind.Utf8 }
            : _encoding;
        var kinds = new[] { (_crLf, LineEndingStyle.CrLf), (_lf, LineEndingStyle.Lf), (_cr, LineEndingStyle.Cr) }
            .Where(pair => pair.Item1 > 0)
            .ToList();
        var lineEnding = kinds.Count switch
        {
            0 => LineEndingStyle.None,
            1 => kinds[0].Item2,
            _ => LineEndingStyle.Mixed,
        };
        var fieldsPerRecord = _fieldCounts.Count == 0
            ? 0
            : _fieldCounts.OrderByDescending(pair => pair.Value).ThenByDescending(pair => pair.Key).First().Key;
        var dataRecords = _columnNames is null ? _records : _records - 1;
        return new CsvReadSummary(dataRecords, encoding, lineEnding, _lastUnitWasLineEnd, fieldsPerRecord, _blankLines, _crLf, _lf, _cr);
    }

    private void Report(string code, IssueSeverity severity, string message, int? column, long? record = null, long? line = null)
    {
        var name = column is { } index && _columnNames is { } names && index <= names.Count ? names[index - 1] : null;
        _issues.Add(new Issue(code, severity, message, new IssueLocation(record ?? CurrentRecord, line ?? _line, column, name)));
    }

    private void ResetRecord()
    {
        _fields.Clear();
        _recordHasContent = false;
        _strayQuoteReportedAt = -1;
    }
}
