using System.Globalization;
using Fedele.Core;

namespace Fedele.Formats.Csv;

/// <summary>The result of looking for the separator in a sample.</summary>
/// <param name="Separator">The chosen separator, or <see langword="null"/> when none splits the records consistently.</param>
/// <param name="FieldCount">The most common number of fields with that separator.</param>
/// <param name="MatchingRecords">Sampled records with exactly <paramref name="FieldCount"/> fields.</param>
/// <param name="SampledRecords">Records examined, blank lines excluded.</param>
/// <param name="TiedWith">Another separator that splits the sample equally well, if any.</param>
internal sealed record SeparatorGuess(char? Separator, int FieldCount, int MatchingRecords, int SampledRecords, char? TiedWith)
{
    /// <summary>Share of sampled records with the most common number of fields.</summary>
    public double Consistency => SampledRecords == 0 ? 0 : (double)MatchingRecords / SampledRecords;

    /// <summary>Turns the guess into a deduction with confidence and reason.</summary>
    public Detected<char?> ToDetected()
    {
        if (Separator is null)
        {
            return new Detected<char?>(
                null,
                SampledRecords == 0 ? 0.3 : 0.5,
                "No candidate separator (; , TAB |) splits the records consistently: the file is read as a single column.");
        }

        var name = CsvLayout.DisplaySeparator(Separator);
        var reason = string.Create(
            CultureInfo.InvariantCulture,
            $"{MatchingRecords} of {SampledRecords} sampled records have {FieldCount} fields separated by {name}.");
        var confidence = Consistency;
        if (SampledRecords < 2)
        {
            confidence = Math.Min(confidence, 0.5);
            reason += " The sample holds a single record.";
        }

        if (TiedWith is not null)
        {
            confidence *= 0.5;
            reason += $" {CsvLayout.DisplaySeparator(TiedWith)} splits them equally well; {name} was chosen, but this is a guess.";
        }

        return new Detected<char?>(Separator, confidence, reason);
    }
}

/// <summary>
/// Chooses the separator that gives the most consistent number of fields per record.
/// </summary>
internal static class SeparatorDetector
{
    private const int MaxRecords = 1000;
    private const double Tolerance = 1e-9;

    /// <summary>The candidates, in order of preference when everything else is equal.</summary>
    public static IReadOnlyList<char> Candidates { get; } = [';', ',', '\t', '|'];

    /// <summary>Looks for the separator in a sample of code units.</summary>
    /// <param name="sample">Code units from the start of the file, byte order mark excluded.</param>
    /// <param name="isWholeFile">True when the sample is the whole file, so its last record is complete.</param>
    /// <param name="quote">The quote character.</param>
    public static SeparatorGuess Detect(IReadOnlyList<int> sample, bool isWholeFile, char quote)
    {
        var results = new List<SeparatorGuess>();
        foreach (var candidate in Candidates)
        {
            var counter = new FieldCountSink();
            var lexer = new CsvLexer(candidate, quote, counter);
            foreach (var unit in sample)
            {
                lexer.Feed(unit);
            }

            if (isWholeFile)
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
            if (fieldCount >= 2)
            {
                results.Add(new SeparatorGuess(candidate, fieldCount, frequency, counter.Counts.Count, null));
            }
        }

        if (results.Count == 0)
        {
            return new SeparatorGuess(null, 1, 0, CountRecords(sample, isWholeFile, quote), null);
        }

        var ordered = results
            .OrderByDescending(guess => guess.Consistency)
            .ThenByDescending(guess => guess.FieldCount)
            .ToList();
        var best = ordered[0];
        var tie = ordered.Skip(1).FirstOrDefault(guess => Math.Abs(guess.Consistency - best.Consistency) <= Tolerance);
        return tie is null ? best : best with { TiedWith = tie.Separator };
    }

    private static int CountRecords(IReadOnlyList<int> sample, bool isWholeFile, char quote)
    {
        var counter = new FieldCountSink();
        var lexer = new CsvLexer(CsvLexer.NoSeparator, quote, counter);
        foreach (var unit in sample)
        {
            lexer.Feed(unit);
        }

        if (isWholeFile)
        {
            lexer.Complete();
        }

        return counter.Counts.Count;
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
            var isBlankLine = _fields == 1 && !_hasContent;
            if (!isBlankLine && Counts.Count < MaxRecords)
            {
                Counts.Add(_fields);
            }

            _fields = 0;
            _hasContent = false;
        }

        public void OnProblem(CsvProblem problem)
        {
        }
    }
}
