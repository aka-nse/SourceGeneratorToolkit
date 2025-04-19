using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace SourceGeneratorToolkit;

internal class StringViewComparer : IEqualityComparer<ReadOnlyMemory<char>>
{
    public static StringViewComparer Default { get; } = new StringViewComparer();

    public bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
        => MemoryExtensions.Equals(x.Span, y.Span, StringComparison.InvariantCulture);

    public int GetHashCode(ReadOnlyMemory<char> obj)
    {
        var vv = MemoryMarshal.Cast<char, Vector<int>>(obj.Span);
        var v = default(Vector<int>);
        for (int i = 0; i < vv.Length; i++)
        {
            v ^= vv[i];
        }
        return v.GetHashCode();
    }
}
