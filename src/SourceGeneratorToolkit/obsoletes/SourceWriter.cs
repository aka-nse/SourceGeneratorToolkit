using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CompilerToolkit.Generators;


public partial class SourceWriter : IEquatable<SourceWriter>
{
    private readonly string[] _usingNamespaces;
    private readonly Dictionary<TypeIdentifier, TypeBuilder> _typeBuilders;
    private readonly Dictionary<TypeIdentifier, string> _typeNames;


    private SourceWriter(
        string[] usingNamespaces,
        Dictionary<TypeIdentifier, TypeBuilder> typeBuilders,
        Dictionary<TypeIdentifier, string> typeNames)
    {
        _usingNamespaces = usingNamespaces;
        _typeBuilders = typeBuilders;
        _typeNames = typeNames;
    }

    public bool Equals(SourceWriter other)
        => Equals(this, other);

    public static bool Equals(SourceWriter? x, SourceWriter? y)
    {
        switch(x, y)
        {
        case (null, null):
            return true;
        case (null, { }):
        case ({ }, null):
            return false;
        default:
            break;
        }
        if (x._usingNamespaces.Length != y._usingNamespaces.Length)
        {
            return false;
        }
        if(x._typeBuilders.Count != y._typeBuilders.Count)
        {
            return false;
        }
        if (x._typeNames.Count != y._typeNames.Count)
        {
            return false;
        }
        return !x._typeNames.Keys.Except(y._typeNames.Keys).Any();
    }

    public ITypeBuilder GetTypeBuilder(TypeIdentifier type)
        => _typeBuilders[type];

    public IEnumerable<TypeIdentifier> GetTypes()
        => _typeBuilders.Keys;

    public string GenerateSource()
    {
        var state = new SourceBuilderState(_typeNames);

        var sb = new StringBuilder();
        foreach(var ns in _usingNamespaces)
        {
            sb.AppendLine($"using {ns};");
        }

        foreach (var type in _typeBuilders.Values)
        {
            type.Generate(state, sb);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}


public interface ISourceBuilderState
{
    public string GetDisplayName(TypeIdentifier type);
}
internal class SourceBuilderState(
    Dictionary<TypeIdentifier, string> displayNames)
     : ISourceBuilderState
{
    public string GetDisplayName(TypeIdentifier type)
        => displayNames[type];
}
