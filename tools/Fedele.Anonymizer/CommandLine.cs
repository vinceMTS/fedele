using System.Globalization;
using System.Security.Cryptography;

namespace Fedele.Anonymizer;

/// <summary>
/// Command-line entry point. Output never contains values read from input files:
/// only structure, counts, shapes and header names.
/// </summary>
internal static class CommandLine
{
    private const int Ok = 0;
    private const int UsageError = 1;
    private const int Failure = 2;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0)
        {
            PrintUsage(output);
            return Ok;
        }

        try
        {
            switch (args[0])
            {
                case "analyze":
                    return Analyze(args[1..], output, error);
                case "anonymize":
                    return Anonymize(args[1..], output, error);
                case "help" or "--help" or "-h":
                    PrintUsage(output);
                    return Ok;
                default:
                    return Usage(error, $"Unknown command: {args[0]}");
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"File error: {exception.Message}");
            return Failure;
        }
    }

    private static int Analyze(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 1)
        {
            return Usage(error, "analyze needs exactly one file or folder.");
        }

        var path = args[0];
        string[] files;
        if (Directory.Exists(path))
        {
            files = [.. Directory.EnumerateFiles(path).Order(StringComparer.OrdinalIgnoreCase)];
            if (files.Length == 0)
            {
                output.WriteLine("No files found.");
                return Ok;
            }
        }
        else if (File.Exists(path))
        {
            files = [path];
        }
        else
        {
            error.WriteLine($"Path not found: {path}");
            return UsageError;
        }

        for (var i = 0; i < files.Length; i++)
        {
            if (i > 0)
            {
                output.WriteLine();
            }

            using var stream = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            AnalysisReport.Write(output, Path.GetFileName(files[i]), FileAnalyzer.Analyze(stream));
        }

        return Ok;
    }

    private static int Anonymize(string[] args, TextWriter output, TextWriter error)
    {
        string? columns = null;
        var seed = 1;
        long? maxRecords = null;
        var hasHeader = true;
        var force = false;
        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--columns" when i + 1 < args.Length:
                    columns = args[++i];
                    break;
                case "--seed" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                    {
                        return Usage(error, "--seed needs an integer.");
                    }

                    break;
                case "--max-records" when i + 1 < args.Length:
                    if (!long.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out var max) || max < 1)
                    {
                        return Usage(error, "--max-records needs a positive integer.");
                    }

                    maxRecords = max;
                    break;
                case "--no-header":
                    hasHeader = false;
                    break;
                case "--force":
                    force = true;
                    break;
                case var option when option.StartsWith("--", StringComparison.Ordinal):
                    return Usage(error, $"Unknown or incomplete option: {option}");
                default:
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count != 2)
        {
            return Usage(error, "anonymize needs an input file and an output file.");
        }

        if (columns is null)
        {
            return Usage(error, "--columns is required. Use --columns none to copy the file unchanged.");
        }

        var (inputPath, outputPath) = (Path.GetFullPath(positional[0]), Path.GetFullPath(positional[1]));
        if (!File.Exists(inputPath))
        {
            error.WriteLine($"Input file not found: {positional[0]}");
            return UsageError;
        }

        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            return Usage(error, "The output file must be different from the input file.");
        }

        if (File.Exists(outputPath) && !force)
        {
            error.WriteLine($"Output file already exists: {positional[1]} (use --force to overwrite)");
            return UsageError;
        }

        HashSet<int> selected;
        using (var input = File.OpenRead(inputPath))
        {
            var resolved = ResolveColumns(columns, hasHeader, input, error);
            if (resolved is null)
            {
                return UsageError;
            }

            selected = resolved;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        AnonymizeResult result;
        using (var input = File.OpenRead(inputPath))
        using (var outputStream = new FileStream(outputPath, force ? FileMode.Create : FileMode.CreateNew, FileAccess.Write))
        {
            result = FileAnonymizer.Anonymize(input, outputStream, new AnonymizeOptions(selected, seed, hasHeader, maxRecords));
        }

        output.WriteLine($"Written    : {positional[1]}");
        output.WriteLine($"Encoding   : {result.Encoding.DisplayName}; BOM: {result.Encoding.BomDisplay}");
        output.WriteLine($"Separator  : {result.Separator.Display}");
        output.WriteLine($"Records    : {result.Records}{(maxRecords is null ? string.Empty : $" (limit: {maxRecords} data records)")}");

        if (selected.Count == 0)
        {
            bool identical;
            if (maxRecords is null)
            {
                var inputHash = Hash(inputPath);
                identical = inputHash == Hash(outputPath);
                output.WriteLine($"Byte-identical copy: {(identical ? "yes" : "NO")} (SHA-256 {inputHash})");
            }
            else
            {
                identical = IsPrefix(outputPath, inputPath);
                output.WriteLine($"Byte-identical prefix of the input: {(identical ? "yes" : "NO")} ({new FileInfo(outputPath).Length} bytes)");
            }

            if (!identical)
            {
                error.WriteLine("The copy differs from the input. This is a bug in Fedele.Anonymizer: do not use the output.");
                return Failure;
            }

            return Ok;
        }

        output.WriteLine($"Seed       : {seed}");
        foreach (var column in selected.Order())
        {
            output.WriteLine($"Column {column + 1,3} : {result.ReplacedValues.GetValueOrDefault(column)} values replaced");
        }

        output.WriteLine("Note: check characters (codice fiscale, partita IVA, IBAN) are not recomputed yet.");
        return Ok;
    }

    // Columns are 1-based numbers or header names, separated by commas; "none" selects no column.
    private static HashSet<int>? ResolveColumns(string columns, bool hasHeader, Stream input, TextWriter error)
    {
        var selected = new HashSet<int>();
        if (columns.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return selected;
        }

        IReadOnlyList<string>? header = null;
        foreach (var token in columns.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                if (number < 1)
                {
                    error.WriteLine($"Column numbers start at 1: {token}");
                    return null;
                }

                selected.Add(number - 1);
                continue;
            }

            if (!hasHeader)
            {
                error.WriteLine($"Column names need a header; with --no-header use column numbers: {token}");
                return null;
            }

            if (header is null)
            {
                var (encoding, separator) = CsvSniffer.Sniff(input);
                header = CsvSniffer.ReadFirstRecord(input, encoding, separator);
            }

            var index = header.ToList().FindIndex(name => name.Equals(token, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                error.WriteLine($"Unknown column: {token}. Use a header name or a 1-based number (see 'analyze').");
                return null;
            }

            selected.Add(index);
        }

        if (selected.Count == 0)
        {
            error.WriteLine("No columns given. Use --columns none to copy the file unchanged.");
            return null;
        }

        return selected;
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static bool IsPrefix(string prefixPath, string fullPath)
    {
        using var prefix = File.OpenRead(prefixPath);
        using var full = File.OpenRead(fullPath);
        if (prefix.Length > full.Length)
        {
            return false;
        }

        var left = new byte[64 * 1024];
        var right = new byte[left.Length];
        int read;
        while ((read = prefix.ReadAtLeast(left, left.Length, throwOnEndOfStream: false)) > 0)
        {
            full.ReadExactly(right, 0, read);
            if (!left.AsSpan(0, read).SequenceEqual(right.AsSpan(0, read)))
            {
                return false;
            }
        }

        return true;
    }

    private static int Usage(TextWriter error, string message)
    {
        error.WriteLine(message);
        error.WriteLine();
        PrintUsage(error);
        return UsageError;
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Fedele.Anonymizer - anonymizes data files while keeping what makes them hard to read.");
        writer.WriteLine();
        writer.WriteLine("Usage:");
        writer.WriteLine("  analyze <file|folder>");
        writer.WriteLine("      Prints only the structure: encoding, BOM, line endings, separator, quoting,");
        writer.WriteLine("      records, header names and the probable type of each column. Never values.");
        writer.WriteLine();
        writer.WriteLine("  anonymize <input> <output> --columns <list|none> [--seed <n>] [--max-records <n>] [--no-header] [--force]");
        writer.WriteLine("      Replaces the values of the listed columns (1-based numbers or header names,");
        writer.WriteLine("      comma-separated) with fake values of the same shape. Every other byte is");
        writer.WriteLine("      copied unchanged. --columns none makes a verified byte-identical copy.");
        writer.WriteLine("      --max-records keeps the header and the first <n> data records, with their line ending.");
    }
}
