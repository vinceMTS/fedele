using System.Globalization;
using Fedele.Core;

namespace Fedele.Formats.Csv;

/// <summary>Reads CSV files in streaming: deduces encoding and separator, and never alters the values.</summary>
public sealed class CsvFormatReader : IFormatReader
{
    /// <summary>The format identifier: <c>csv</c>.</summary>
    public const string Id = "csv";

    /// <summary>Size of the sample read when a file is opened.</summary>
    public const int SampleSize = 64 * 1024;

    /// <inheritdoc/>
    public string FormatId => Id;

    /// <inheritdoc/>
    public FormatDetection Detect(ReadOnlySpan<byte> sample)
    {
        if (sample.IsEmpty)
        {
            return new FormatDetection(Id, 0.1, "The file is empty.");
        }

        var encoding = TextEncodingDetector.Detect(sample, isWholeFile: false).Value;
        var text = sample[encoding.BomLength..];
        if (encoding.CodeUnitSize == 1 && text.Contains((byte)0))
        {
            return new FormatDetection(Id, 0, "The file contains zero bytes: it is binary, not text.");
        }

        var guess = SeparatorDetector.Detect(CodeUnits.Read(text, encoding), isWholeFile: false, quote: '"');
        if (guess.Separator is null)
        {
            return new FormatDetection(Id, 0.3, "Text without a recognizable separator: at best a single-column CSV.");
        }

        var reason = string.Create(
            CultureInfo.InvariantCulture,
            $"{guess.MatchingRecords} of {guess.SampledRecords} records have {guess.FieldCount} fields separated by {CsvLayout.DisplaySeparator(guess.Separator)}.");
        return new FormatDetection(Id, 0.5 + (0.45 * guess.Consistency), reason);
    }

    /// <inheritdoc/>
    /// <remarks>Pass a <see cref="CsvReadOptions"/> to set the separator or the quote.</remarks>
    public async ValueTask<IRecordReader> OpenAsync(Stream input, ReadOptions? options = null, CancellationToken cancellationToken = default) =>
        await OpenCsvAsync(input, CsvReadOptions.From(options), cancellationToken).ConfigureAwait(false);

    /// <summary>Opens a CSV file; like <see cref="OpenAsync"/>, with CSV options.</summary>
    /// <exception cref="ArgumentException">The separator or the quote is a line break, or they are the same character.</exception>
    public async ValueTask<IRecordReader> OpenCsvAsync(Stream input, CsvReadOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        options ??= new CsvReadOptions();
        Validate(options);
        return await CsvRecordReader.OpenAsync(input, options, cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(CsvReadOptions options)
    {
        if (options.Quote is '\r' or '\n' || options.Separator is '\r' or '\n')
        {
            throw new ArgumentException("The separator and the quote cannot be line breaks.", nameof(options));
        }

        if (options.Separator == options.Quote)
        {
            throw new ArgumentException("The separator and the quote must be different characters.", nameof(options));
        }
    }
}

/// <summary>Registers the CSV format in Fedele.</summary>
public static class CsvFedeleBuilderExtensions
{
    /// <summary>Adds the CSV format.</summary>
    public static FedeleBuilder AddCsv(this FedeleBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddFormat(new CsvFormatReader());
    }
}

/// <summary>Turns bytes into the code units read by <see cref="CsvLexer"/>.</summary>
internal static class CodeUnits
{
    /// <summary>Reads the whole units in <paramref name="bytes"/>; a last odd byte of UTF-16 is ignored.</summary>
    public static List<int> Read(ReadOnlySpan<byte> bytes, TextEncodingInfo encoding)
    {
        var units = new List<int>(bytes.Length / encoding.CodeUnitSize);
        if (encoding.CodeUnitSize == 1)
        {
            foreach (var b in bytes)
            {
                units.Add(b);
            }

            return units;
        }

        for (var i = 0; i + 1 < bytes.Length; i += 2)
        {
            units.Add(Combine(bytes[i], bytes[i + 1], encoding));
        }

        return units;
    }

    /// <summary>Combines two bytes of UTF-16 into one unit.</summary>
    public static int Combine(byte first, byte second, TextEncodingInfo encoding) =>
        encoding.Kind == TextEncodingKind.Utf16BigEndian ? (first << 8) | second : (second << 8) | first;
}
