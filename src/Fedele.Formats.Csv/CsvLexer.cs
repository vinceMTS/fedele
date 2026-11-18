namespace Fedele.Formats.Csv;

/// <summary>Role of a code unit in the CSV structure.</summary>
internal enum UnitRole
{
    /// <summary>Part of a field value, including escaped quotes and line breaks inside quotes.</summary>
    Content,

    /// <summary>An opening or closing quote of a quoted field.</summary>
    Quote,

    /// <summary>A field separator.</summary>
    Separator,

    /// <summary>A CR or LF that ends a record.</summary>
    LineEnd,
}

/// <summary>A malformation found by <see cref="CsvLexer"/>. The lexer keeps reading: it never fails.</summary>
internal enum CsvProblem
{
    /// <summary>A quoted field still open at the end of the file.</summary>
    UnterminatedQuote,

    /// <summary>Text after the closing quote of a field, as in <c>"abc"def</c>.</summary>
    TextAfterClosingQuote,

    /// <summary>A quote in the middle of an unquoted field, as in <c>ab"c</c>.</summary>
    StrayQuote,
}

/// <summary>Receives the structure found by <see cref="CsvLexer"/>.</summary>
internal interface ICsvSink
{
    /// <summary>Called for every unit, in input order.</summary>
    void OnUnit(int unit, UnitRole role);

    /// <summary>Called when a field ends, before its separator or line end.</summary>
    void OnFieldEnd(bool quoted);

    /// <summary>Called when a record ends, before its line end units.</summary>
    void OnRecordEnd();

    /// <summary>Called when a malformation is found, while the field it belongs to is still open.</summary>
    void OnProblem(CsvProblem problem);
}

/// <summary>
/// A tolerant, streaming CSV state machine: RFC 4180 plus the malformations found in real files.
/// It works on code units (bytes, or 16-bit values for UTF-16), in which separators, quotes and
/// line breaks are always single units, so the structure is found without decoding the text.
/// </summary>
internal sealed class CsvLexer(int separator, int quote, ICsvSink sink)
{
    /// <summary>A separator value that never matches, for files with a single column.</summary>
    public const int NoSeparator = -2;

    private const int Cr = '\r';
    private const int Lf = '\n';

    private State _state = State.RecordStart;
    private bool _quoted;

    private enum State
    {
        RecordStart,
        FieldStart,
        Unquoted,
        Quoted,
        QuoteInQuoted,
        AfterClosingQuote,
        AfterCr,
    }

    /// <summary>Processes the next unit.</summary>
    public void Feed(int unit)
    {
        switch (_state)
        {
            case State.AfterCr:
                _state = State.RecordStart;
                if (unit == Lf)
                {
                    sink.OnUnit(unit, UnitRole.LineEnd);
                    return;
                }

                StartField(unit);
                return;

            case State.RecordStart:
            case State.FieldStart:
                StartField(unit);
                return;

            case State.Quoted:
                if (unit == quote)
                {
                    _state = State.QuoteInQuoted;
                }
                else
                {
                    sink.OnUnit(unit, UnitRole.Content);
                }

                return;

            case State.QuoteInQuoted:
                if (unit == quote)
                {
                    // An escaped quote: both units belong to the value, which keeps one of them.
                    sink.OnUnit(quote, UnitRole.Content);
                    sink.OnUnit(unit, UnitRole.Quote);
                    _state = State.Quoted;
                    return;
                }

                sink.OnUnit(quote, UnitRole.Quote);
                _state = State.AfterClosingQuote;
                Unquoted(unit);
                return;

            default:
                Unquoted(unit);
                return;
        }
    }

    /// <summary>Signals the end of the input.</summary>
    public void Complete()
    {
        switch (_state)
        {
            case State.RecordStart:
            case State.AfterCr:
                break;

            case State.Quoted:
                sink.OnProblem(CsvProblem.UnterminatedQuote);
                EndField();
                sink.OnRecordEnd();
                break;

            case State.QuoteInQuoted:
                sink.OnUnit(quote, UnitRole.Quote);
                EndField();
                sink.OnRecordEnd();
                break;

            default:
                EndField();
                sink.OnRecordEnd();
                break;
        }

        _state = State.RecordStart;
    }

    private void StartField(int unit)
    {
        if (unit == quote)
        {
            _quoted = true;
            sink.OnUnit(unit, UnitRole.Quote);
            _state = State.Quoted;
            return;
        }

        Unquoted(unit);
    }

    private void Unquoted(int unit)
    {
        if (unit == separator)
        {
            EndField();
            sink.OnUnit(unit, UnitRole.Separator);
            _state = State.FieldStart;
            return;
        }

        if (unit is Cr or Lf)
        {
            EndField();
            sink.OnRecordEnd();
            sink.OnUnit(unit, UnitRole.LineEnd);
            _state = unit == Cr ? State.AfterCr : State.RecordStart;
            return;
        }

        if (_state == State.AfterClosingQuote)
        {
            sink.OnProblem(CsvProblem.TextAfterClosingQuote);
        }
        else if (unit == quote)
        {
            sink.OnProblem(CsvProblem.StrayQuote);
        }

        sink.OnUnit(unit, UnitRole.Content);
        _state = State.Unquoted;
    }

    private void EndField()
    {
        sink.OnFieldEnd(_quoted);
        _quoted = false;
    }
}
