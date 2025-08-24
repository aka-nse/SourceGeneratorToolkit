using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace SourceGeneratorToolkit;

/// <summary>
/// Represents a interpolated string handler that can be used to build source code.<br/>
/// This type supports a special formatting type:
/// <list type="table">
///     <item>
///         <term>
///             <see cref="INamedTypeSymbol"/><br/>
///         </term>
///         <description>
///             will be formatted into the qualified name as well as short.
///         </description>
///     </item>
///     <item>
///         <term>
///             <see cref="IEnumerable{T}"/> with <see cref="CodePartExtensions.PreserveIndent{T}(IEnumerable{T})"/> operator<br/>
///         </term>
///         <description>
///             will capture the indentation preceding the current position in the interpolated string,
///             and appends it to each element in the output.
///         </description>
///     </item>
/// </list>
/// </summary>
/// <param name="literalLength"></param>
/// <param name="formattedCount"></param>
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
    {
        switch (value)
        {
        case string literal:
            _codeParts.Add(new LiteralCodePart(literal.AsMemory()));
            break;
        case INamedTypeSymbol typeSymbol:
            _codeParts.Add(new TypeSymbolCodePart(typeSymbol));
            break;
        default:
            _codeParts.Add(new FormattedCodePart<T>(value));
            break;
        }
    }

    public void AppendFormatted<T>(T value, int alignment)
    {
        if (value is INamedTypeSymbol typeSymbol)
        {
            _codeParts.Add(new TypeSymbolCodePart(typeSymbol, alignment));
        }
        else
        {
            _codeParts.Add(new FormattedCodePart<T>(value, alignment));
        }
    }

    public void AppendFormatted<T>(T value, string format)
        => _codeParts.Add(new FormattedCodePart<T>(value, format: format));

    public void AppendFormatted<T>(T value, int alignment, string format)
        => _codeParts.Add(new FormattedCodePart<T>(value, alignment, format));
}
