namespace Fedele.Core;

/// <summary>
/// A value deduced by Fedele, with how sure Fedele is about it and why.
/// </summary>
/// <typeparam name="T">Type of the deduced value.</typeparam>
public sealed record Detected<T>
{
    /// <summary>Creates a deduction.</summary>
    /// <param name="value">The deduced value.</param>
    /// <param name="confidence">How sure Fedele is, from 0 (a guess) to 1 (certain).</param>
    /// <param name="reason">Why Fedele chose this value, in a sentence a person can read.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="confidence"/> is not between 0 and 1.</exception>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is empty.</exception>
    public Detected(T value, double confidence, string reason)
    {
        if (double.IsNaN(confidence) || confidence < 0 || confidence > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Confidence must be between 0 and 1.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Value = value;
        Confidence = confidence;
        Reason = reason;
    }

    /// <summary>The deduced value.</summary>
    public T Value { get; }

    /// <summary>How sure Fedele is, from 0 (a guess) to 1 (certain).</summary>
    public double Confidence { get; }

    /// <summary>Why Fedele chose this value, in a sentence a person can read.</summary>
    public string Reason { get; }
}
