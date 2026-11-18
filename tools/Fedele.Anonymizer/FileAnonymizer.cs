using System.Globalization;
using System.Text.RegularExpressions;

namespace Fedele.Anonymizer;

/// <summary>
/// Options for <see cref="FileAnonymizer"/>. Columns are 0-based. When <paramref name="MaxDataRecords"/>
/// is set, the output stops after that many data records (header excluded) and their line ending.
/// </summary>
internal sealed record AnonymizeOptions(IReadOnlySet<int> Columns, int Seed, bool HasHeader, long? MaxDataRecords = null);

/// <summary>What <see cref="FileAnonymizer"/> did. Replaced counts are per 0-based column.</summary>
internal sealed record AnonymizeResult(
    DetectedEncoding Encoding,
    SeparatorGuess Separator,
    long Records,
    IReadOnlyDictionary<int, long> ReplacedValues);

/// <summary>
/// Rewrites a CSV file replacing the values of chosen columns with fake values of the same shape.
/// Every other byte (BOM, separators, quotes, line endings, other columns) is copied unchanged.
/// </summary>
internal static class FileAnonymizer
{
    /// <summary>Anonymizes a seekable input stream into the output stream.</summary>
    public static AnonymizeResult Anonymize(Stream input, Stream output, AnonymizeOptions options)
    {
        var (encoding, separator) = CsvSniffer.Sniff(input);

        input.Position = 0;
        Span<byte> bom = stackalloc byte[encoding.BomLength];
        input.ReadExactly(bom);

        var buffered = new BufferedStream(output, 64 * 1024);
        buffered.Write(bom);

        var reader = new UnitReader(input, encoding);
        var sink = new RewriteSink(buffered, encoding, options);
        var lexer = new CsvLexer(separator.Unit, CsvSniffer.Quote, sink);
        int unit;
        while (!sink.Stopped && (unit = reader.Read()) >= 0)
        {
            lexer.Feed(unit);
        }

        if (!sink.Stopped)
        {
            lexer.Complete();
            if (reader.TrailingByte is { } trailing)
            {
                buffered.WriteByte((byte)trailing);
            }
        }

        buffered.Flush();
        return new AnonymizeResult(encoding, separator, sink.Records, sink.Replaced);
    }

    private sealed class RewriteSink(Stream output, DetectedEncoding encoding, AnonymizeOptions options) : ICsvSink
    {
        private readonly Random _random = new(options.Seed);
        private readonly List<(int Unit, UnitRole Role)> _field = [];
        private readonly List<int> _run = [];
        private bool _headerPending = options.HasHeader;
        private bool _recordHasContent;
        private int _column;
        private long _dataRecords;
        private bool _limitReached;
        private bool _sawCrAfterLimit;

        public long Records { get; private set; }

        public Dictionary<int, long> Replaced { get; } = [];

        /// <summary>True once the record limit and its line ending have been written.</summary>
        public bool Stopped { get; private set; }

        private bool ReplacesCurrentField => !_headerPending && options.Columns.Contains(_column);

        public void OnUnit(int unit, UnitRole role)
        {
            if (_limitReached)
            {
                WriteFinalLineEnd(unit, role);
                return;
            }

            var isFieldUnit = role is UnitRole.Content or UnitRole.Quote;

            // A record with a separator is a real record even when all its fields are empty (",,,,"),
            // exactly as analyze counts it; only a truly empty line is not.
            _recordHasContent |= isFieldUnit || role == UnitRole.Separator;
            if (isFieldUnit && ReplacesCurrentField)
            {
                _field.Add((unit, role));
                return;
            }

            UnitCodec.Write(unit, encoding, output);
        }

        public void OnFieldEnd(bool quoted)
        {
            if (_field.Count > 0)
            {
                FlushField();
            }

            _column++;
        }

        public void OnRecordEnd()
        {
            if (_recordHasContent)
            {
                Records++;
                _dataRecords += _headerPending ? 0 : 1;
                _headerPending = false;
                _limitReached = _dataRecords == options.MaxDataRecords;
            }

            _recordHasContent = false;
            _column = 0;
        }

        // After the last kept record, only its own line ending (CR, LF or CRLF) is written.
        private void WriteFinalLineEnd(int unit, UnitRole role)
        {
            if (Stopped || role != UnitRole.LineEnd)
            {
                Stopped = true;
                return;
            }

            if (unit == '\r' && !_sawCrAfterLimit)
            {
                _sawCrAfterLimit = true;
                UnitCodec.Write(unit, encoding, output);
                return;
            }

            if (unit == '\n')
            {
                UnitCodec.Write(unit, encoding, output);
            }

            Stopped = true;
        }

        // Quotes are written as they are; each run of content between them is scrambled.
        private void FlushField()
        {
            var replaced = false;
            foreach (var (unit, role) in _field)
            {
                if (role == UnitRole.Content)
                {
                    _run.Add(unit);
                    continue;
                }

                replaced |= WriteRun();
                UnitCodec.Write(unit, encoding, output);
            }

            replaced |= WriteRun();
            if (replaced)
            {
                Replaced[_column] = Replaced.GetValueOrDefault(_column) + 1;
            }

            _field.Clear();
        }

        private bool WriteRun()
        {
            if (_run.Count == 0)
            {
                return false;
            }

            var scrambled = ValueScrambler.Scramble(UnitCodec.Decode(_run, encoding), _random);
            output.Write(encoding.TextEncoding.GetBytes(scrambled));
            _run.Clear();
            return true;
        }
    }
}

/// <summary>
/// Produces a fake value with the same shape as the original: digits stay digits (zeros stay zeros,
/// so leading zeros survive), letters stay letters of the same case, everything else is unchanged.
/// Dates become other valid dates with the same pattern and the same day/month evidence.
/// </summary>
internal static class ValueScrambler
{
    /// <summary>Scrambles a value deterministically for a given random generator.</summary>
    public static string Scramble(string value, Random random)
    {
        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            start++;
        }

        var end = value.Length;
        while (end > start && char.IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        var chars = value.ToCharArray();
        if (!TryScrambleDate(value[start..end], chars, start, random))
        {
            for (var i = start; i < end; i++)
            {
                chars[i] = ScrambleChar(chars[i], random);
            }
        }

        return new string(chars);
    }

    private static char ScrambleChar(char c, Random random)
    {
        if (c is >= '1' and <= '9')
        {
            return (char)('1' + random.Next(9));
        }

        if (char.IsLetter(c))
        {
            return char.IsUpper(c) ? (char)('A' + random.Next(26)) : (char)('a' + random.Next(26));
        }

        return c;
    }

    private static bool TryScrambleDate(string core, char[] chars, int offset, Random random)
    {
        var dayMonthYear = DatePatterns.DayMonthYear().Match(core);
        if (dayMonthYear.Success && DatePatterns.IsValidDayMonthYear(dayMonthYear, out _))
        {
            var groups = dayMonthYear.Groups;
            Put(chars, offset, groups[1], DayOrMonth(groups[1].Value, random));
            Put(chars, offset, groups[3], DayOrMonth(groups[3].Value, random));
            Put(chars, offset, groups[4], Year(groups[4].Value, random));
            PutTime(chars, offset, groups[5], groups[6], groups[7], random);
            return true;
        }

        var iso = DatePatterns.Iso().Match(core);
        if (iso.Success && DatePatterns.IsValidIso(iso))
        {
            var groups = iso.Groups;
            Put(chars, offset, groups[1], Year(groups[1].Value, random));
            Put(chars, offset, groups[2], Two(random.Next(1, 13)));
            Put(chars, offset, groups[3], Two(random.Next(1, 29)));
            PutTime(chars, offset, groups[4], groups[5], groups[6], random);
            return true;
        }

        return false;
    }

    // Keeps the width and the evidence: a part above 12 can only be a day, so it stays above 12;
    // a part up to 12 stays up to 12. Days never exceed 28, so the date is valid in either order.
    private static string DayOrMonth(string original, Random random)
    {
        if (original.Length == 1)
        {
            return Digit(random.Next(1, 10));
        }

        if (original[0] == '0')
        {
            return "0" + Digit(random.Next(1, 10));
        }

        var value = int.Parse(original, CultureInfo.InvariantCulture);
        return Two(value > 12 ? random.Next(13, 29) : random.Next(10, 13));
    }

    private static string Year(string original, Random random) =>
        original.Length == 4 ? random.Next(1940, 2026).ToString(CultureInfo.InvariantCulture) : Two(random.Next(0, 100));

    private static void PutTime(char[] chars, int offset, Group hours, Group minutes, Group seconds, Random random)
    {
        if (!hours.Success)
        {
            return;
        }

        var newHours = hours.Length == 1
            ? Digit(random.Next(0, 10))
            : hours.Value[0] == '0' ? "0" + Digit(random.Next(0, 10)) : Two(random.Next(10, 24));
        Put(chars, offset, hours, newHours);
        Put(chars, offset, minutes, Two(random.Next(0, 60)));
        if (seconds.Success)
        {
            Put(chars, offset, seconds, Two(random.Next(0, 60)));
        }
    }

    private static void Put(char[] chars, int offset, Group group, string replacement) =>
        replacement.CopyTo(chars.AsSpan(offset + group.Index, group.Length));

    private static string Digit(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Two(int value) => value.ToString("D2", CultureInfo.InvariantCulture);
}
