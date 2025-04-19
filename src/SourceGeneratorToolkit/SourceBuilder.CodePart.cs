using Microsoft.CodeAnalysis;
using FrozenSB = SourceGeneratorToolkit.FrozenSourceBuilder;

namespace SourceGeneratorToolkit;

partial struct SourceBuilder
{
    internal abstract class CodePart
    {
        private CodePart()
        {
        }

        public abstract void Frozen(FrozenState state);


        private sealed class LiteralCodePart(ReadOnlyMemory<char> s, bool linebreak) : CodePart
        {
            public override void Frozen(FrozenState state)
                => state.AddCodePart(FrozenSB.CodePart.Literal(s, linebreak));
        }

        private sealed class TypeSymbolCodePart(INamedTypeSymbol typeSymbol) : CodePart
        {
            public override void Frozen(FrozenState state)
            {
                var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                state.AddType(typeSymbol);
                state.AddCodePart(FrozenSB.CodePart.TypeSymbol(fullName));
            }
        }

        private sealed class FormattedCodePart<T>(T value) : CodePart
        {
            public override void Frozen(FrozenState state)
                => state.AddCodePart(FrozenSB.CodePart.Formatted<T>(value));
        }


        internal static CodePart Linebreak { get; }
            = new LiteralCodePart(ReadOnlyMemory<char>.Empty, true);

        public static CodePart Literal(ReadOnlyMemory<char> s, bool linebreak)
            => new LiteralCodePart(s, linebreak);

        public static CodePart TypeSymbol(INamedTypeSymbol typeSymbol)
            => new TypeSymbolCodePart(typeSymbol);

        public static CodePart Formatted<T>(T value)
            => new FormattedCodePart<T>(value);
    }


}
