namespace Fedele.Anonymizer;

/// <summary>
/// Reads a stream as a sequence of code units: bytes for single-byte and UTF-8 files,
/// 16-bit values for UTF-16. Units map back to the original bytes exactly, and the
/// characters that matter for CSV structure (separators, quotes, line breaks) are
/// always single units, so the structure can be read without decoding.
/// </summary>
internal sealed class UnitReader(Stream stream, DetectedEncoding encoding)
{
    private readonly byte[] _buffer = new byte[64 * 1024];
    private int _length;
    private int _position;

    /// <summary>A last byte that does not form a whole UTF-16 unit, if any.</summary>
    public int? TrailingByte { get; private set; }

    /// <summary>Returns the next unit, or -1 at the end of the stream.</summary>
    public int Read()
    {
        var unitSize = encoding.UnitSize;
        if (!EnsureAvailable(unitSize))
        {
            if (_length - _position == 1 && unitSize == 2)
            {
                TrailingByte = _buffer[_position];
                _position++;
            }

            return -1;
        }

        if (unitSize == 1)
        {
            return _buffer[_position++];
        }

        int first = _buffer[_position], second = _buffer[_position + 1];
        _position += 2;
        return encoding.IsBigEndian ? (first << 8) | second : (second << 8) | first;
    }

    private bool EnsureAvailable(int count)
    {
        while (_length - _position < count)
        {
            if (_position > 0)
            {
                Buffer.BlockCopy(_buffer, _position, _buffer, 0, _length - _position);
                _length -= _position;
                _position = 0;
            }

            var read = stream.Read(_buffer, _length, _buffer.Length - _length);
            if (read == 0)
            {
                return false;
            }

            _length += read;
        }

        return true;
    }
}

/// <summary>Converts code units back to bytes and text.</summary>
internal static class UnitCodec
{
    /// <summary>Writes one unit to the output exactly as it was in the input.</summary>
    public static void Write(int unit, DetectedEncoding encoding, Stream output)
    {
        if (encoding.UnitSize == 1)
        {
            output.WriteByte((byte)unit);
        }
        else if (encoding.IsBigEndian)
        {
            output.WriteByte((byte)(unit >> 8));
            output.WriteByte((byte)unit);
        }
        else
        {
            output.WriteByte((byte)unit);
            output.WriteByte((byte)(unit >> 8));
        }
    }

    /// <summary>Decodes a sequence of units to text.</summary>
    public static string Decode(List<int> units, DetectedEncoding encoding)
    {
        if (units.Count == 0)
        {
            return string.Empty;
        }

        var bytes = new byte[units.Count * encoding.UnitSize];
        using (var stream = new MemoryStream(bytes))
        {
            foreach (var unit in units)
            {
                Write(unit, encoding, stream);
            }
        }

        return encoding.TextEncoding.GetString(bytes);
    }
}
