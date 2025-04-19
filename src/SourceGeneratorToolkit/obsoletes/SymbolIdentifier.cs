using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace CompilerToolkit.Generators;


public interface ITypeIdentifier : IEquatable<ITypeIdentifier>
{
    public string Name { get; }
    public string FullName { get; }
    public string GetDisplayName(ISourceBuilderState state);
}

public record TypeIdentifier(
    string? ContainingNamespace,
    TypeIdentifier? ContainingType,
    string Name,
    TypeKind TypeKind,
    bool IsRecord,
    bool IsFileOnly)
    : ITypeIdentifier, IEquatable<TypeIdentifier>
{
    public string FullName => _fullName ??= GetFullName();
    private string? _fullName;

    public int GenericTypeArgumentsCount
        => Name.Split('`').ElementAtOrDefault(1) is string genericCount
            ? int.Parse(genericCount)
            : 0;

    public TypeIdentifier(INamedTypeSymbol symbol)
        : this(
            GetContainingNamespace(symbol.ContainingNamespace),
            TryGetInfo(symbol.ContainingType),
            symbol.Name,
            symbol.TypeKind,
            symbol.IsRecord,
            true)
    {
    }

    public override int GetHashCode()
        => StringComparer.InvariantCulture.GetHashCode(FullName);

    public virtual bool Equals(TypeIdentifier? other)
        => other is { } && StringComparer.InvariantCulture.Equals(FullName, other.FullName);

    bool IEquatable<ITypeIdentifier>.Equals(ITypeIdentifier? other)
        => other is TypeIdentifier typeIdentifier
            && Equals(this, typeIdentifier);

    public override string ToString() => FullName;

    public string GetDisplayName(ISourceBuilderState state)
        => state.GetDisplayName(this);


    private string GetFullName()
    {
        if (ContainingType is { })
        {
            return $"{ContainingType.GetFullName()}.{Name}";
        }
        else
        {
            var ns = string.IsNullOrEmpty(ContainingNamespace)
                ? $"{ContainingNamespace}."
                : "";
            return $"{ns}{Name}";
        }
    }


    public static IEnumerable<TypeIdentifier> Ancestors(TypeIdentifier type)
    {
        if (type.ContainingType is null)
        {
            yield break;
        }
        foreach(var ancestor in Ancestors(type.ContainingType))
        {
            yield return ancestor;
        }
        yield return type.ContainingType;
    }


    private static string GetContainingNamespace(INamespaceSymbol? symbol)
        => symbol is null || symbol.IsGlobalNamespace
        ? string.Empty
        : symbol.ToDisplayString();


    private static TypeIdentifier? TryGetInfo(INamedTypeSymbol? symbol)
        => symbol is INamedTypeSymbol containingType
            ? new TypeIdentifier(containingType)
            : null;
}


public record GenericTypeArgumentIdentifier(
    string Name) : ITypeIdentifier
{
    public string FullName => Name;

    public override int GetHashCode() => Name.GetHashCode();

    public virtual bool Equals(GenericTypeArgumentIdentifier? other)
        => other is not null
            && Name == other.Name;

    bool IEquatable<ITypeIdentifier>.Equals(ITypeIdentifier other)
        => other is GenericTypeArgumentIdentifier otherGeneric
            && Name == otherGeneric.Name;

    public override string ToString() => FullName;

    public string GetDisplayName(ISourceBuilderState state)
        => Name;
}


public record GenericTypeIdentifier : ITypeIdentifier
{
    public TypeIdentifier Original { get; }
    public ImmutableArray<ITypeIdentifier> TypeArguments { get; }

    public string Name
        => $"{Original.Name.Split('`')[0]}<{string.Join(", ", TypeArguments.Select(x => x.Name))}>";

    public string FullName
        => $"{Original.FullName.Split('`')[0]}<{string.Join(", ", TypeArguments.Select(x => x.FullName))}>";

    public GenericTypeIdentifier(
        TypeIdentifier original,
        ImmutableArray<ITypeIdentifier> typeArguments)
    {
        if(!original.Name.Contains('`'))
        {
            throw new ArgumentException("TypeIdentifier is not generic", nameof(original));
        }
        Original = original;
        TypeArguments = typeArguments;
    }

    public override int GetHashCode() => FullName.GetHashCode();

    public virtual bool Equals(GenericTypeIdentifier? other)
        => other is not null
            && Original.Equals(other.Original)
            && TypeArguments.SequenceEqual(other.TypeArguments);

    bool IEquatable<ITypeIdentifier>.Equals(ITypeIdentifier other)
        => other is GenericTypeIdentifier otherGeneric && Equals(otherGeneric);

    public override string ToString() => FullName;

    public string GetDisplayName(ISourceBuilderState state)
        => $"{Original.GetDisplayName(state)}<{string.Join(", ", TypeArguments.Select(t => t.GetDisplayName(state)))}>";
}


public sealed record ParameterIdentifier(ITypeIdentifier Type, string Name);
