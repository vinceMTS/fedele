using System.Globalization;
using System.Reflection;
using Fedele.Core;
using Fedele.Formats.Csv;

namespace Fedele.Cli;

/// <summary>
/// The fedele command line. <c>inspect</c> shows structure, deductions, counts and issues:
/// never the values of the records.
/// </summary>
internal static class CommandLine
{
    public const int Ok = 0;
    public const int UsageError = 1;
    public const int Failure = 2;

    private const int ShownIssues = 20;

    private static readonly FedeleEngine _engine = new FedeleBuilder().AddCsv().Build();

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintUsage(output);
            return Ok;
        }

        try
        {
            return args[0] switch
            {
                "inspect" => await InspectAsync(args[1..], output, error, cancellationToken),
                "--version" => PrintVersion(output),
                _ => Usage(error, $"Unknown command: {args[0]}"),
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"File error: {exception.Message}");
            return Failure;
        }
    }

    private static async Task<int> InspectAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        string? path = null;
        var options = new CsvReadOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--no-header":
                    options = options with { Header = HeaderMode.Absent };
                    break;
                case "--separator" when i + 1 < args.Length:
                    if (ParseSeparator(args[++i]) is not { } separator)
                    {
                        return Usage(error, $"Invalid separator: {args[i]} (use one character, or 'tab').");
                    }

                    options = options with { Separator = separator };
                    break;
                case "--encoding" when i + 1 < args.Length:
                    if (ParseEncoding(args[++i]) is not { } encoding)
                    {
                        return Usage(error, $"Unknown encoding: {args[i]} (use ascii, utf-8, utf-16le, utf-16be or windows-1252).");
                    }

                    options = options with { Encoding = encoding };
                    break;
                case var arg when arg.StartsWith('-'):
                    return Usage(error, $"Unknown or incomplete option: {arg}");
                case var arg when path is null:
                    path = arg;
                    break;
                default:
                    return Usage(error, "inspect needs exactly one file.");
            }
        }

        if (path is null)
        {
            return Usage(error, "inspect needs a file.");
        }

        if (!File.Exists(path))
        {
            error.WriteLine($"File not found: {path}");
            return Failure;
        }

        await using var stream = File.OpenRead(path);
        var sample = new byte[FedeleEngine.DetectionSampleSize];
        var length = await stream.ReadAtLeastAsync(sample, sample.Length, throwOnEndOfStream: false, cancellationToken);
        var detection = _engine.Detect(sample.AsSpan(0, length));
        if (detection is null)
        {
            error.WriteLine("Fedele does not recognize the format of this file.");
            return Failure;
        }

        stream.Position = 0;
        await using var reader = await _engine.OpenAsync(stream, options, detection.FormatId, cancellationToken);

        output.WriteLine($"File: {path}");
        output.WriteLine(Invariant($"Format: {detection.FormatId} (confidence {detection.Confidence:0.00}: {detection.Reason})"));
        output.WriteLine();
        WriteFacts(output, "Layout", reader.Layout.Describe());

        if (reader.Layout.ColumnNames is { } names)
        {
            output.WriteLine(Invariant($"Columns ({names.Count})"));
            for (var i = 0; i < names.Count; i++)
            {
                output.WriteLine(Invariant($"  {i + 1,3}  {names[i]}"));
            }

            output.WriteLine();
        }

        await foreach (var _ in reader.ReadRecordsAsync(cancellationToken))
        {
        }

        WriteFacts(output, "Summary", reader.Summary!.Describe());
        WriteIssues(output, reader.Issues, reader.DroppedIssueCount);
        return Ok;
    }

    private static void WriteFacts(TextWriter output, string title, IReadOnlyList<LayoutFact> facts)
    {
        output.WriteLine(title);
        var nameWidth = facts.Max(fact => fact.Name.Length);
        var valueWidth = facts.Max(fact => fact.Value.Length);
        foreach (var fact in facts)
        {
            var line = $"  {fact.Name.PadRight(nameWidth)}  {fact.Value.PadRight(valueWidth)}";
            if (fact.Confidence is { } confidence)
            {
                line += Invariant($"  {confidence:0.00}");
            }

            if (fact.Reason is { } reason)
            {
                line += (fact.Confidence is null ? "        " : "  ") + reason;
            }

            output.WriteLine(line.TrimEnd());
        }

        output.WriteLine();
    }

    private static void WriteIssues(TextWriter output, IReadOnlyList<Issue> issues, long dropped)
    {
        var total = issues.Count + dropped;
        if (total == 0)
        {
            output.WriteLine("Issues: none");
            return;
        }

        output.WriteLine(Invariant($"Issues ({total})"));
        foreach (var group in issues.GroupBy(issue => issue.Code).OrderByDescending(group => group.Count()))
        {
            output.WriteLine(Invariant($"  {group.Count(),8}  {group.Key}"));
        }

        if (dropped > 0)
        {
            output.WriteLine(Invariant($"  {dropped,8}  more, beyond the limit of issues kept in memory"));
        }

        output.WriteLine();
        foreach (var issue in issues.Take(ShownIssues))
        {
            output.WriteLine($"  {SeverityName(issue.Severity),-7}  {issue.Code}  {Where(issue.Location)}: {issue.Message}");
        }

        if (total > ShownIssues)
        {
            output.WriteLine(Invariant($"  ... {total - ShownIssues} more not shown."));
        }
    }

    private static string Where(IssueLocation? location)
    {
        if (location is null)
        {
            return "file";
        }

        var where = Invariant($"record {location.Record}, line {location.Line}");
        if (location.Column is { } column)
        {
            where += Invariant($", column {column}");
            if (location.ColumnName is { } name)
            {
                where += $" ({name})";
            }
        }

        return where;
    }

    private static string SeverityName(IssueSeverity severity) => severity switch
    {
        IssueSeverity.Error => "error",
        IssueSeverity.Warning => "warning",
        _ => "info",
    };

    private static char? ParseSeparator(string value) => value switch
    {
        "tab" or "TAB" or "\\t" => '\t',
        { Length: 1 } when value[0] is not ('\r' or '\n') => value[0],
        _ => null,
    };

    private static TextEncodingKind? ParseEncoding(string value) =>
        Enum.GetValues<TextEncodingKind>()
            .Select(kind => (TextEncodingKind?)kind)
            .FirstOrDefault(kind => string.Equals(TextEncodingInfo.IdOf(kind!.Value), value, StringComparison.OrdinalIgnoreCase));

    private static int PrintVersion(TextWriter output)
    {
        output.WriteLine($"fedele {Version}");
        return Ok;
    }

    private static string Version =>
        typeof(CommandLine).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    private static void PrintUsage(TextWriter output)
    {
        output.WriteLine($"fedele {Version}: reads data files without betraying their content.");
        output.WriteLine();
        output.WriteLine("Usage:");
        output.WriteLine("  fedele inspect <file> [--no-header] [--separator <char|tab>] [--encoding <id>]");
        output.WriteLine("      Shows what Fedele deduces about the file, with confidence and reasons,");
        output.WriteLine("      a summary and the issues found. It never prints the values of the records.");
        output.WriteLine("  fedele --version");
        output.WriteLine();
        output.WriteLine("Encodings: ascii, utf-8, utf-16le, utf-16be, windows-1252.");
        output.WriteLine("Coming later: infer, validate, convert.");
    }

    private static int Usage(TextWriter error, string message)
    {
        error.WriteLine(message);
        error.WriteLine("Run 'fedele --help' for usage.");
        return UsageError;
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
