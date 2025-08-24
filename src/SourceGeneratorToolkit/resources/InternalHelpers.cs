using System;
using System.Text;

namespace SourceGeneratorToolkit;

internal static class InternalHelpers
{
    [ThreadStatic]
    private static char[] _buffer = default!;

    public static void Append(this StringBuilder sb, ReadOnlySpan<char> value)
    {
        static int getNewLen(int length)
        {
            length -= 1;
            length |= length >> 1;
            length |= length >> 2;
            length |= length >> 4;
            length |= length >> 8;
            length |= length >> 16;
            return length + 1;
        }

        if (_buffer is null || _buffer.Length < value.Length)
        {
            _buffer = new char[getNewLen(value.Length)];
        }
        value.CopyTo(_buffer.AsSpan(0, value.Length));
        sb.Append(_buffer, 0, value.Length);
    }
}
