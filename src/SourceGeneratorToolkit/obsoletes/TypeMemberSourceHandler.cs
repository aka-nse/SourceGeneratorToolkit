using System.Runtime.CompilerServices;
using System.Text;

namespace CompilerToolkit.Generators;

[InterpolatedStringHandler]
public ref partial struct TypeMemberSourceHandler(int literalLength, int formattedCount)
{
    public readonly int LiteralLength => literalLength;

    internal readonly IReadOnlyList<CodePart> CodeParts => _codeParts ?? [];
    private readonly List<CodePart> _codeParts = new (formattedCount * 2 + 1);

    public void AppendLiteral(string s)
        => _codeParts.Add(new LiteralCodePart(s));

    public void AppendFormatted(ITypeIdentifier typeSymbol)
        => _codeParts.Add(new TypeSymbolCodePart(typeSymbol));

    public void AppendFormatted<T>(T value)
    {
        CodePart part = value is ITypeIdentifier typeIdentifier
            ? new TypeSymbolCodePart(typeIdentifier)
            : new FormattedCodePart<T>(value);
        _codeParts.Add(part);
    }
}
