namespace Fedele.Anonymizer;

/// <summary>A decoded field. <see cref="Length"/> is in characters, or in units when the field was too long to decode.</summary>
internal readonly record struct FieldValue(string Text, bool Quoted, bool HasLineBreak, long Length);

/// <summary>Structural statistics of one column. Holds counts and shapes only, never values.</summary>
internal sealed class ColumnStats
{
    private readonly Dictionary<ValueKind, long> _kinds = [];
    private readonly Dictionary<string, long> _datePatterns = [];

    public long Values { get; private set; }

    public IReadOnlyDictionary<ValueKind, long> Kinds => _kinds;

    public IReadOnlyDictionary<string, long> DatePatterns => _datePatterns;

    public long DayFirstProofs { get; private set; }

    public long MonthFirstProofs { get; private set; }

    public long LeadingZeros { get; private set; }

    public long SurroundingWhitespace { get; private set; }

    public long Quoted { get; private set; }

    public long LineBreaks { get; private set; }

    public long MaxLength { get; private set; }

    /// <summary>The most frequent non-empty kind, or <see cref="ValueKind.Empty"/>.</summary>
    public ValueKind ProbableKind => _kinds
        .Where(pair => pair.Key != ValueKind.Empty)
        .OrderByDescending(pair => pair.Value)
        .Select(pair => pair.Key)
        .DefaultIfEmpty(ValueKind.Empty)
        .First();

    public void Add(FieldValue field)
    {
        Values++;
        Quoted += field.Quoted ? 1 : 0;
        LineBreaks += field.HasLineBreak ? 1 : 0;
        MaxLength = Math.Max(MaxLength, field.Length);

        var trimmed = field.Text.Trim();
        if (trimmed.Length > 0 && trimmed.Length != field.Text.Length)
        {
            SurroundingWhitespace++;
        }

        var value = ValueClassifier.Classify(trimmed);
        _kinds[value.Kind] = _kinds.GetValueOrDefault(value.Kind) + 1;
        if (value.Pattern is not null)
        {
            _datePatterns[value.Pattern] = _datePatterns.GetValueOrDefault(value.Pattern) + 1;
        }

        DayFirstProofs += value.Order == DateOrder.DayFirst ? 1 : 0;
        MonthFirstProofs += value.Order == DateOrder.MonthFirst ? 1 : 0;
        LeadingZeros += value.LeadingZero ? 1 : 0;
    }
}

/// <summary>The structure of a file: everything <c>analyze</c> reports, and nothing else.</summary>
internal sealed class FileAnalysis
{
    public required long SizeBytes { get; init; }

    public required DetectedEncoding Encoding { get; init; }

    public required SeparatorGuess Separator { get; init; }

    public required long CrLf { get; init; }

    public required long Lf { get; init; }

    public required long Cr { get; init; }

    public required bool EndsWithLineEnding { get; init; }

    /// <summary>Non-empty records, header included.</summary>
    public required long Records { get; init; }

    public required long EmptyRecords { get; init; }

    public required IReadOnlyDictionary<int, long> FieldCounts { get; init; }

    /// <summary>Field count of the first non-empty record.</summary>
    public required int ExpectedFieldCount { get; init; }

    /// <summary>1-based numbers (empty records included) of the first records with a different field count.</summary>
    public required IReadOnlyList<long> IrregularRecords { get; init; }

    public required long QuotedFields { get; init; }

    public required long FieldsWithLineBreaks { get; init; }

    public required long UnterminatedQuotes { get; init; }

    public required long TextAfterClosingQuote { get; init; }

    public required long StrayQuotes { get; init; }

    /// <summary>Column names, only when the first record looks like a header; otherwise null.</summary>
    public required IReadOnlyList<string>? HeaderNames { get; init; }

    public required IReadOnlyList<ColumnStats> Columns { get; init; }
}

/// <summary>Analyzes the structure of a CSV file in streaming.</summary>
internal static class FileAnalyzer
{
    /// <summary>Analyzes a seekable stream.</summary>
    public static FileAnalysis Analyze(Stream stream)
    {
        var (encoding, separator) = CsvSniffer.Sniff(stream);
        stream.Position = encoding.BomLength;
        var reader = new UnitReader(stream, encoding);
        var sink = new AnalysisSink(encoding);
        var lexer = new CsvLexer(separator.Unit, CsvSniffer.Quote, sink);
        int unit;
        while ((unit = reader.Read()) >= 0)
        {
            lexer.Feed(unit);
        }

        lexer.Complete();

        return new FileAnalysis
        {
            SizeBytes = stream.Length,
            Encoding = encoding,
            Separator = separator,
            CrLf = sink.CrLf,
            Lf = sink.Lf,
            Cr = sink.Cr,
            EndsWithLineEnding = sink.EndsWithLineEnding,
            Records = sink.Records,
            EmptyRecords = sink.EmptyRecords,
            FieldCounts = sink.FieldCounts,
            ExpectedFieldCount = sink.ExpectedFieldCount,
            IrregularRecords = sink.IrregularRecords,
            QuotedFields = sink.QuotedFields,
            FieldsWithLineBreaks = sink.FieldsWithLineBreaks,
            UnterminatedQuotes = lexer.UnterminatedQuotes,
            TextAfterClosingQuote = lexer.TextAfterClosingQuote,
            StrayQuotes = lexer.StrayQuotes,
            HeaderNames = sink.HeaderNames,
            Columns = sink.Columns,
        };
    }

    /// <summary>
    /// A first record is treated as a header only if every field is a distinct, non-empty,
    /// short piece of text. Otherwise its names are never shown, since they could be data.
    /// </summary>
    internal static bool IsHeaderLike(IReadOnlyList<FieldValue> record)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in record)
        {
            var name = field.Text.Trim();
            var plausible = name.Length is > 0 and <= 100
                && !field.HasLineBreak
                && ValueClassifier.Classify(name).Kind == ValueKind.Text
                && seen.Add(name);
            if (!plausible)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class AnalysisSink(DetectedEncoding encoding) : ICsvSink
    {
        private const int CrUnit = '\r';
        private const int LfUnit = '\n';
        private const int MaxDecodedUnits = 4096;
        private const int MaxIrregularRecords = 5;

        private readonly List<int> _units = [];
        private readonly List<FieldValue> _record = [];
        private long _fieldLength;
        private bool _fieldHasLineBreak;
        private bool _recordHasContent;
        private bool _previousWasCr;
        private bool _headerDecided;
        private long _recordNumber;

        public long CrLf { get; private set; }

        public long Lf { get; private set; }

        public long Cr { get; private set; }

        public bool EndsWithLineEnding { get; private set; }

        public long Records { get; private set; }

        public long EmptyRecords { get; private set; }

        public Dictionary<int, long> FieldCounts { get; } = [];

        public int ExpectedFieldCount { get; private set; }

        public List<long> IrregularRecords { get; } = [];

        public long QuotedFields { get; private set; }

        public long FieldsWithLineBreaks { get; private set; }

        public List<string>? HeaderNames { get; private set; }

        public List<ColumnStats> Columns { get; } = [];

        public void OnUnit(int unit, UnitRole role)
        {
            switch (role)
            {
                case UnitRole.Content:
                    _recordHasContent = true;
                    _fieldLength++;
                    _fieldHasLineBreak |= unit is CrUnit or LfUnit;
                    if (_units.Count < MaxDecodedUnits)
                    {
                        _units.Add(unit);
                    }

                    break;

                case UnitRole.Quote:
                    _recordHasContent = true;
                    break;

                case UnitRole.LineEnd when unit == LfUnit && _previousWasCr:
                    Cr--;
                    CrLf++;
                    break;

                case UnitRole.LineEnd when unit == CrUnit:
                    Cr++;
                    break;

                case UnitRole.LineEnd:
                    Lf++;
                    break;
            }

            _previousWasCr = role == UnitRole.LineEnd && unit == CrUnit;
            EndsWithLineEnding = role == UnitRole.LineEnd;
        }

        public void OnFieldEnd(bool quoted)
        {
            var text = UnitCodec.Decode(_units, encoding);
            var length = _fieldLength <= MaxDecodedUnits ? text.Length : _fieldLength;
            _record.Add(new FieldValue(text, quoted, _fieldHasLineBreak, length));
            QuotedFields += quoted ? 1 : 0;
            FieldsWithLineBreaks += _fieldHasLineBreak ? 1 : 0;

            _units.Clear();
            _fieldLength = 0;
            _fieldHasLineBreak = false;
        }

        public void OnRecordEnd()
        {
            _recordNumber++;
            if (_record.Count == 1 && !_recordHasContent)
            {
                EmptyRecords++;
            }
            else
            {
                Records++;
                FieldCounts[_record.Count] = FieldCounts.GetValueOrDefault(_record.Count) + 1;
                if (!_headerDecided)
                {
                    _headerDecided = true;
                    ExpectedFieldCount = _record.Count;
                    if (IsHeaderLike(_record))
                    {
                        HeaderNames = [.. _record.Select(field => field.Text.Trim())];
                    }
                    else
                    {
                        AddData();
                    }
                }
                else
                {
                    if (_record.Count != ExpectedFieldCount && IrregularRecords.Count < MaxIrregularRecords)
                    {
                        IrregularRecords.Add(_recordNumber);
                    }

                    AddData();
                }
            }

            _record.Clear();
            _recordHasContent = false;
        }

        private void AddData()
        {
            for (var i = 0; i < _record.Count; i++)
            {
                while (Columns.Count <= i)
                {
                    Columns.Add(new ColumnStats());
                }

                Columns[i].Add(_record[i]);
            }
        }
    }
}
