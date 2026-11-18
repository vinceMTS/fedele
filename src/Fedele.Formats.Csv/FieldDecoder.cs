using System.Text;
using System.Text.Unicode;
using Fedele.Core;

namespace Fedele.Formats.Csv;

/// <summary>
/// Collects the code units of one field and turns them into text, telling whether they were valid.
/// Invalid bytes become U+FFFD in the text and are reported by the caller: never fixed silently.
/// </summary>
internal sealed class FieldDecoder(TextEncodingInfo encoding)
{
    private const char ReplacementCharacter = '�';

    private byte[] _bytes = new byte[256];
    private char[] _chars = new char[128];
    private int _length;

    /// <summary>True once a byte above 127 was seen in a single-byte or UTF-8 file.</summary>
    public bool SawNonAscii { get; private set; }

    /// <summary>Adds one code unit of the field's value.</summary>
    public void Append(int unit)
    {
        if (encoding.CodeUnitSize == 2)
        {
            if (_length == _chars.Length)
            {
                Array.Resize(ref _chars, _chars.Length * 2);
            }

            _chars[_length++] = (char)unit;
            return;
        }

        if (_length == _bytes.Length)
        {
            Array.Resize(ref _bytes, _bytes.Length * 2);
        }

        _bytes[_length++] = (byte)unit;
        SawNonAscii |= unit >= 0x80;
    }

    /// <summary>Returns the field's text and starts a new field.</summary>
    /// <param name="valid">False when some units are not valid in the encoding.</param>
    public string TakeText(out bool valid)
    {
        valid = true;
        if (_length == 0)
        {
            return string.Empty;
        }

        string text;
        if (encoding.CodeUnitSize == 2)
        {
            var chars = _chars.AsSpan(0, _length);
            valid = IsWellFormedUtf16(chars);
            text = valid ? new string(chars) : ReplaceLoneSurrogates(chars);
        }
        else
        {
            var bytes = _bytes.AsSpan(0, _length);
            if (encoding.Kind != TextEncodingKind.Windows1252)
            {
                valid = Utf8.IsValid(bytes);
            }

            text = encoding.Encoding.GetString(bytes);
        }

        _length = 0;
        return text;
    }

    private static bool IsWellFormedUtf16(ReadOnlySpan<char> chars)
    {
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsHighSurrogate(chars[i]) && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(chars[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static string ReplaceLoneSurrogates(ReadOnlySpan<char> chars)
    {
        var builder = new StringBuilder(chars.Length);
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsHighSurrogate(chars[i]) && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1]))
            {
                builder.Append(chars[i]).Append(chars[i + 1]);
                i++;
            }
            else
            {
                builder.Append(char.IsSurrogate(chars[i]) ? ReplacementCharacter : chars[i]);
            }
        }

        return builder.ToString();
    }
}
