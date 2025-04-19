using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGeneratorToolkit;

internal static class InternalHelpers
{
    public static void AppendJoin(this StringBuilder sb, string separator, IEnumerable<string> values)
    {
        var iter = values.GetEnumerator();
        if (!iter.MoveNext())
        {
            return;
        }
        sb.Append(iter.Current);
        while (iter.MoveNext())
        {
            sb.Append(separator);
            sb.Append(iter.Current);
        }
    }

    public static unsafe void Append(this StringBuilder sb, ReadOnlySpan<char> value)
    {
        fixed(char* p = value)
        {
            sb.Append(p, value.Length);
        }
    }
}
