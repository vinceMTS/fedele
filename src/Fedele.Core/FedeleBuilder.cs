namespace Fedele.Core;

/// <summary>
/// Composes Fedele from the modules an application chooses, such as <c>new FedeleBuilder().AddCsv().Build()</c>.
/// </summary>
public sealed class FedeleBuilder
{
    private readonly List<IFormatReader> _formats = [];

    /// <summary>Adds a file format. Format modules call this from their own extension method, such as <c>AddCsv()</c>.</summary>
    /// <exception cref="InvalidOperationException">A format with the same identifier was already added.</exception>
    public FedeleBuilder AddFormat(IFormatReader format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (_formats.Any(existing => string.Equals(existing.FormatId, format.FormatId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"The format '{format.FormatId}' was already added.");
        }

        _formats.Add(format);
        return this;
    }

    /// <summary>Creates the configured <see cref="FedeleEngine"/>.</summary>
    public FedeleEngine Build() => new([.. _formats]);
}

/// <summary>Fedele with the modules chosen by the application. Create it with <see cref="FedeleBuilder"/>.</summary>
public sealed class FedeleEngine
{
    /// <summary>Size of the sample used to recognize the format of a file.</summary>
    public const int DetectionSampleSize = 64 * 1024;

    internal FedeleEngine(IReadOnlyList<IFormatReader> formats) => Formats = formats;

    /// <summary>The formats this engine can read.</summary>
    public IReadOnlyList<IFormatReader> Formats { get; }

    /// <summary>Returns the format with the given identifier, or <see langword="null"/>.</summary>
    public IFormatReader? FindFormat(string formatId) =>
        Formats.FirstOrDefault(format => string.Equals(format.FormatId, formatId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the most likely format for a file starting with <paramref name="sample"/>,
    /// or <see langword="null"/> when no format recognizes it.
    /// </summary>
    public FormatDetection? Detect(ReadOnlySpan<byte> sample)
    {
        FormatDetection? best = null;
        foreach (var format in Formats)
        {
            var detection = format.Detect(sample);
            if (detection.Confidence > 0 && (best is null || detection.Confidence > best.Confidence))
            {
                best = detection;
            }
        }

        return best;
    }

    /// <summary>
    /// Opens a file. When <paramref name="formatId"/> is <see langword="null"/>, the format is recognized
    /// from the first bytes. The stream does not need to be seekable.
    /// </summary>
    /// <exception cref="NotSupportedException">No configured format can read the file.</exception>
    public async ValueTask<IRecordReader> OpenAsync(
        Stream input,
        ReadOptions? options = null,
        string? formatId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (formatId is not null)
        {
            var chosen = FindFormat(formatId) ?? throw new NotSupportedException($"The format '{formatId}' is not configured.");
            return await chosen.OpenAsync(input, options, cancellationToken).ConfigureAwait(false);
        }

        var sample = new byte[DetectionSampleSize];
        var length = await input.ReadAtLeastAsync(sample, sample.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        var detection = Detect(sample.AsSpan(0, length))
            ?? throw new NotSupportedException("No configured format recognizes this file.");
        var format = FindFormat(detection.FormatId)!;
        var replayed = new PrefixedStream(sample.AsMemory(0, length), input);
        return await format.OpenAsync(replayed, options, cancellationToken).ConfigureAwait(false);
    }
}
