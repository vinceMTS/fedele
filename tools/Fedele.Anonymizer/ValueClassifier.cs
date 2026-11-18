using System.Globalization;
using System.Text.RegularExpressions;

namespace Fedele.Anonymizer;

/// <summary>The shape of a single value.</summary>
public enum ValueKind
{
    Empty,
    Integer,
    DecimalComma,
    DecimalDot,
    AmbiguousNumber,
    Date,
    DateTime,
    TaxCode,
    VatNumber,
    Iban,
    Email,
    Phone,
    Text,
}

/// <summary>For dates like 05/03/2020: which part is the day, as far as this value proves.</summary>
public enum DateOrder
{
    NotApplicable,
    Ambiguous,
    DayFirst,
    MonthFirst,
}

/// <summary>The classification of one value. <see cref="Pattern"/> is a shape such as <c>nn/nn/nnnn</c>, never a value.</summary>
internal readonly record struct ValueClass(
    ValueKind Kind,
    string? Pattern = null,
    DateOrder Order = DateOrder.NotApplicable,
    bool LeadingZero = false);

/// <summary>
/// Guesses the kind of a value from its shape only. Check digits are verified for
/// partita IVA and IBAN; codice fiscale is recognized by shape.
/// </summary>
internal static partial class ValueClassifier
{
    /// <summary>Classifies a value that has already been trimmed.</summary>
    public static ValueClass Classify(string value)
    {
        if (value.Length == 0)
        {
            return new ValueClass(ValueKind.Empty);
        }

        if (TaxCodeRegex().IsMatch(value))
        {
            return new ValueClass(ValueKind.TaxCode);
        }

        if (IsIban(value))
        {
            return new ValueClass(ValueKind.Iban);
        }

        if (IsVatNumber(value))
        {
            return new ValueClass(ValueKind.VatNumber);
        }

        if (EmailRegex().IsMatch(value))
        {
            return new ValueClass(ValueKind.Email);
        }

        var dayMonthYear = DatePatterns.DayMonthYear().Match(value);
        if (dayMonthYear.Success)
        {
            // Shaped like a date but impossible (45/67/2020): honestly text, never a number or phone.
            if (!DatePatterns.IsValidDayMonthYear(dayMonthYear, out var order))
            {
                return new ValueClass(ValueKind.Text);
            }

            var kind = dayMonthYear.Groups[5].Success ? ValueKind.DateTime : ValueKind.Date;
            return new ValueClass(kind, DatePatterns.Shape(value), order);
        }

        var iso = DatePatterns.Iso().Match(value);
        if (iso.Success && DatePatterns.IsValidIso(iso))
        {
            var kind = iso.Groups[4].Success ? ValueKind.DateTime : ValueKind.Date;
            return new ValueClass(kind, DatePatterns.Shape(value));
        }

        if (value[0] == '+' && IsPhone(value))
        {
            return new ValueClass(ValueKind.Phone);
        }

        var integer = IntegerRegex().Match(value);
        if (integer.Success)
        {
            var digits = integer.Groups[1].Value;
            return new ValueClass(ValueKind.Integer, LeadingZero: digits.Length > 1 && digits[0] == '0');
        }

        if (AmbiguousNumberRegex().IsMatch(value))
        {
            return new ValueClass(ValueKind.AmbiguousNumber);
        }

        if (DecimalCommaRegex().IsMatch(value))
        {
            return new ValueClass(ValueKind.DecimalComma);
        }

        if (DecimalDotRegex().IsMatch(value))
        {
            return new ValueClass(ValueKind.DecimalDot);
        }

        return IsPhone(value) ? new ValueClass(ValueKind.Phone) : new ValueClass(ValueKind.Text);
    }

    /// <summary>Human-readable name of a kind.</summary>
    public static string Describe(ValueKind kind) => kind switch
    {
        ValueKind.Empty => "empty",
        ValueKind.Integer => "integer",
        ValueKind.DecimalComma => "number 1.234,56",
        ValueKind.DecimalDot => "number 1,234.56",
        ValueKind.AmbiguousNumber => "number ambiguous (1.234 / 1,234)",
        ValueKind.Date => "date",
        ValueKind.DateTime => "date and time",
        ValueKind.TaxCode => "codice fiscale (shape)",
        ValueKind.VatNumber => "partita IVA (check digit ok)",
        ValueKind.Iban => "IBAN (check digits ok)",
        ValueKind.Email => "email",
        ValueKind.Phone => "phone",
        _ => "text",
    };

    // https://www.agenziaentrate.gov.it : 6 letters, 2 digits, letter, 2 digits, letter, 3 digits, letter;
    // digits may be replaced by LMNPQRSTUV (omocodia).
    [GeneratedRegex("^[A-Za-z]{6}[0-9LMNPQRSTUVlmnpqrstuv]{2}[A-Za-z][0-9LMNPQRSTUVlmnpqrstuv]{2}[A-Za-z][0-9LMNPQRSTUVlmnpqrstuv]{3}[A-Za-z]$")]
    private static partial Regex TaxCodeRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^[+-]?(\d+)$")]
    private static partial Regex IntegerRegex();

    // 1.234 or 1,234: thousands separator or decimal separator, the value alone cannot tell.
    [GeneratedRegex(@"^[+-]?\d{1,3}[.,]\d{3}$")]
    private static partial Regex AmbiguousNumberRegex();

    [GeneratedRegex(@"^[+-]?(?:\d{1,3}(?:\.\d{3})+(?:,\d+)?|\d+,\d+)$")]
    private static partial Regex DecimalCommaRegex();

    [GeneratedRegex(@"^[+-]?(?:\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+\.\d+)$")]
    private static partial Regex DecimalDotRegex();

    private static bool IsPhone(string value)
    {
        int digits = 0, separators = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsAsciiDigit(c))
            {
                digits++;
            }
            else if (c is ' ' or '.' or '/' or '-' or '(' or ')')
            {
                separators++;
            }
            else if (!(c == '+' && i == 0))
            {
                return false;
            }
        }

        return digits is >= 8 and <= 15 && (value[0] == '+' || separators > 0);
    }

    // ISO 13616: move the first 4 characters to the end, letters become 10..35, remainder mod 97 must be 1.
    private static bool IsIban(string value)
    {
        Span<char> buffer = stackalloc char[34];
        var length = 0;
        foreach (var c in value)
        {
            if (c == ' ')
            {
                continue;
            }

            if (length == buffer.Length)
            {
                return false;
            }

            buffer[length++] = c;
        }

        if (length < 15)
        {
            return false;
        }

        var iban = buffer[..length];
        if (!char.IsAsciiLetterUpper(iban[0]) || !char.IsAsciiLetterUpper(iban[1])
            || !char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3]))
        {
            return false;
        }

        var remainder = 0;
        for (var i = 0; i < length; i++)
        {
            var c = iban[(i + 4) % length];
            if (char.IsAsciiDigit(c))
            {
                remainder = ((remainder * 10) + (c - '0')) % 97;
            }
            else if (char.IsAsciiLetterUpper(c))
            {
                remainder = ((remainder * 100) + (c - 'A' + 10)) % 97;
            }
            else
            {
                return false;
            }
        }

        return remainder == 1;
    }

    // Partita IVA: 11 digits, the last is a Luhn-style check digit.
    private static bool IsVatNumber(string value)
    {
        var digits = value.StartsWith("IT", StringComparison.Ordinal) ? value.AsSpan(2) : value.AsSpan();
        if (digits.Length != 11 || digits.IndexOfAnyExceptInRange('0', '9') >= 0)
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var digit = digits[i] - '0';
            if (i % 2 == 1)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
        }

        return (10 - (sum % 10)) % 10 == digits[10] - '0';
    }
}

/// <summary>Date shapes shared by the classifier and the scrambler.</summary>
internal static partial class DatePatterns
{
    /// <summary>dd/mm/yyyy, mm/dd/yyyy, with / . or - and an optional time.</summary>
    [GeneratedRegex(@"^(\d{1,2})([/.\-])(\d{1,2})\2(\d{4}|\d{2})(?:[ T](\d{1,2}):(\d{2})(?::(\d{2}))?)?$")]
    public static partial Regex DayMonthYear();

    /// <summary>yyyy-mm-dd with an optional time, fraction and zone.</summary>
    [GeneratedRegex(@"^(\d{4})-(\d{2})-(\d{2})(?:[T ](\d{2}):(\d{2})(?::(\d{2})(?:\.\d+)?)?(?:Z|[+\-]\d{2}:?\d{2})?)?$")]
    public static partial Regex Iso();

    /// <summary>True when the match is a real date in at least one order; tells which orders are possible.</summary>
    public static bool IsValidDayMonthYear(Match match, out DateOrder order)
    {
        var first = Number(match.Groups[1]);
        var second = Number(match.Groups[3]);
        var dayFirst = first is >= 1 and <= 31 && second is >= 1 and <= 12;
        var monthFirst = first is >= 1 and <= 12 && second is >= 1 and <= 31;
        order = dayFirst && monthFirst ? DateOrder.Ambiguous : dayFirst ? DateOrder.DayFirst : DateOrder.MonthFirst;
        return (dayFirst || monthFirst) && IsValidTime(match.Groups[5], match.Groups[6], match.Groups[7]);
    }

    /// <summary>True when the ISO match has a valid month, day and time.</summary>
    public static bool IsValidIso(Match match) =>
        Number(match.Groups[2]) is >= 1 and <= 12
        && Number(match.Groups[3]) is >= 1 and <= 31
        && IsValidTime(match.Groups[4], match.Groups[5], match.Groups[6]);

    /// <summary>The shape of a value: every digit becomes <c>n</c>.</summary>
    public static string Shape(string value) =>
        string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsAsciiDigit(source[i]) ? 'n' : source[i];
            }
        });

    private static bool IsValidTime(Group hours, Group minutes, Group seconds) =>
        !hours.Success
        || (Number(hours) < 24 && Number(minutes) < 60 && (!seconds.Success || Number(seconds) < 60));

    private static int Number(Group group) => int.Parse(group.ValueSpan, CultureInfo.InvariantCulture);
}
