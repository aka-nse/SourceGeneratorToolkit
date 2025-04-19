using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace SourceGeneratorToolkit;

public partial class FrozenSourceBuilder : IEquatable<FrozenSourceBuilder>
{
    public readonly ref struct BuilderState(ImmutableDictionary<ReadOnlyMemory<char>, string> typeNameMap)
    {
        public readonly string GetDisplayName(ReadOnlyMemory<char> fullName)
            => typeNameMap[fullName];
    }


    private readonly ImmutableArray<CodePart> _codeParts;
    private readonly ImmutableDictionary<ReadOnlyMemory<char>, string> _typeNameMap;

    internal FrozenSourceBuilder(
        ImmutableArray<CodePart> codeParts,
        ImmutableDictionary<ReadOnlyMemory<char>, string> typeNameMap)
    {
        _codeParts = codeParts;
        _typeNameMap = typeNameMap;
    }

    public string Build()
    {
        var sb = new StringBuilder();
        var state = new BuilderState(_typeNameMap);
        foreach(var part in _codeParts)
        {
            part.Generate(state, sb);
        }
        return sb.ToString();
    }

    public bool Equals(FrozenSourceBuilder other)
    {
        if(other is null)
        {
            return false;
        }
        if (_codeParts.Length != other._codeParts.Length)
        {
            return false;
        }
        return _codeParts.SequenceEqual(other._codeParts);
    }
}

