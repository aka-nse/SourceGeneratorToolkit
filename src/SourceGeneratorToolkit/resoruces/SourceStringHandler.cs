using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace SourceGeneratorToolkit;

[InterpolatedStringHandler]
internal readonly struct SourceStringHandler(int literalLength, int formattedCount)
{
    public readonly int LiteralLength => literalLength;

    public readonly IReadOnlyList<CodePart> CodeParts => _codeParts ?? [];
    private readonly List<CodePart> _codeParts = new(formattedCount * 2 + 1);

    public void AppendLiteral(string s) => _codeParts.Add(new LiteralCodePart(s.AsMemory()));

    public void AppendFormatted(CodePart codePart)
        => _codeParts.Add(codePart);

    public void AppendFormatted(IEnumerable<CodePart> codeParts)
        => _codeParts.AddRange(codeParts);

    public void AppendFormatted(INamedTypeSymbol typeSymbol)
        => _codeParts.Add(new TypeSymbolCodePart(typeSymbol));

    public void AppendFormatted(INamedTypeSymbol typeSymbol, int alignment)
        => _codeParts.Add(new TypeSymbolCodePart(typeSymbol, alignment));

    public void AppendFormatted(SourceStringHandler other)
        => _codeParts.AddRange(other.CodeParts);

    public void AppendFormatted<T>(T value)
        => _codeParts.Add(new FormattedCodePart<T>(value));

    public void AppendFormatted<T>(T value, int alignment)
        => _codeParts.Add(new FormattedCodePart<T>(value, alignment));

    public void AppendFormatted<T>(T value, string format)
        => _codeParts.Add(new FormattedCodePart<T>(value, format: format));

    public void AppendFormatted<T>(T value, int alignment, string format)
        => _codeParts.Add(new FormattedCodePart<T>(value, alignment, format));
}
