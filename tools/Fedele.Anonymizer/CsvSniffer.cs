namespace Fedele.Anonymizer;

/// <summary>The separator chosen for a file and how consistent it is.</summary>
internal sealed record SeparatorGuess(char? Separator, double Consistency, int SampledRecords)
{
    /// <summary>The separator as a unit for <see cref="CsvLexer"/>.</summary>
    public int Unit => Separator ?? CsvSniffer.NoSeparator;

    /// <summary>Human-readable separator.</summary>
    public string Display => Separator switch
    {
        null => "none detected (single column)",
        '\t' => "TAB",
        var c => $"'{c}'",
    };
}

/// <summary>Detects encoding and separator of a CSV file.</summary>
internal static class CsvSniffer
{
    /// <summary>The quote character.</summary>
    public const int Quote = '"';

    /// <summary>A unit value that never matches, used when there is no separator.</summary>
    public const int NoSeparator = -2;

    private const int SampleUnits = 64 * 1024;

    /// <summary>Detects encoding and separator of a seekable stream.</summary>
    public static (DetectedEncoding Encoding, SeparatorGuess Separator) Sniff(Stream stream)
    {
        var encoding = EncodingDetector.Detect(stream);
        stream.Position = encoding.BomLength;
        var reader = new UnitReader(stream, encoding);
        var sample = new List<int>(SampleUnits);
        int unit;
        while (sample.Count < SampleUnits && (unit = reader.Read()) >= 0)
        {
            sample.Add(unit);
        }

        var sampleIsComplete = sample.Count < SampleUnits;
        return (encoding, SeparatorDetector.Detect(sample, sampleIsComplete));
    }

    /// <summary>Reads the fields of the first non-empty record.</summary>
    public static IReadOnlyList<string> ReadFirstRecord(Stream stream, DetectedEncoding encoding, SeparatorGuess separator)
    {
        stream.Position = encoding.BomLength;
        var reader = new UnitReader(stream, encoding);
        var sink = new FirstRecordSink(encoding);
        var lexer = new CsvLexer(separator.Unit, Quote, sink);
        int unit;
        while (sink.Result is null && (unit = reader.Read()) >= 0)
        {
            lexer.Feed(unit);
        }

        if (sink.Result is null)
        {
            lexer.Complete();
        }

        return sink.Result ?? [];
    }

    private sealed class FirstRecordSink(DetectedEncoding encoding) : ICsvSink
    {
        private readonly List<int> _units = [];
        private readonly List<string> _fields = [];
        private bool _hasContent;

        public IReadOnlyList<string>? Result { get; private set; }

        public void OnUnit(int unit, UnitRole role)
        {
            if (role == UnitRole.Content)
            {
                _units.Add(unit);
            }

            _hasContent |= role is UnitRole.Content or UnitRole.Quote;
        }

        public void OnFieldEnd(bool quoted)
        {
            _fields.Add(UnitCodec.Decode(_units, encoding).Trim());
            _units.Clear();
        }

        public void OnRecordEnd()
        {
            if (Result is null && _hasContent)
            {
                Result = [.. _fields];
            }

            _fields.Clear();
            _hasContent = false;
        }
    }
}

/// <summary>
/// Chooses the separator that gives the most consistent number of fields per record.
/// </summary>
internal static class SeparatorDetector
{
    private const int MaxRecords = 1000;
    private static readonly char[] _candidates = [';', ',', '\t', '|'];

    /// <summary>Detects the separator in a sample of units.</summary>
    public static SeparatorGuess Detect(IReadOnlyList<int> sample, bool sampleIsComplete)
    {
        var best = new SeparatorGuess(null, 0, 0);
        var bestFieldCount = 0;
        foreach (var candidate in _candidates)
        {
            var counter = new FieldCountSink();
            var lexer = new CsvLexer(candidate, CsvSniffer.Quote, counter);
            foreach (var unit in sample)
            {
                lexer.Feed(unit);
            }

            if (sampleIsComplete)
            {
                lexer.Complete();
            }

            if (counter.Counts.Count == 0)
            {
                continue;
            }

            var (fieldCount, frequency) = counter.Counts
                .GroupBy(count => count)
                .Select(group => (FieldCount: group.Key, Frequency: group.Count()))
                .OrderByDescending(pair => pair.Frequency)
                .ThenByDescending(pair => pair.FieldCount)
                .First();
            if (fieldCount < 2)
            {
                continue;
            }

            var consistency = (double)frequency / counter.Counts.Count;
            var better = consistency > best.Consistency + 1e-9
                || (Math.Abs(consistency - best.Consistency) <= 1e-9 && fieldCount > bestFieldCount);
            if (better)
            {
                best = new SeparatorGuess(candidate, consistency, counter.Counts.Count);
                bestFieldCount = fieldCount;
            }
        }

        return best;
    }

    private sealed class FieldCountSink : ICsvSink
    {
        private int _fields;
        private bool _hasContent;

        public List<int> Counts { get; } = [];

        public void OnUnit(int unit, UnitRole role) => _hasContent |= role is UnitRole.Content or UnitRole.Quote;

        public void OnFieldEnd(bool quoted) => _fields++;

        public void OnRecordEnd()
        {
            var isEmptyLine = _fields == 1 && !_hasContent;
            if (!isEmptyLine && Counts.Count < MaxRecords)
            {
                Counts.Add(_fields);
            }

            _fields = 0;
            _hasContent = false;
        }
    }
}
