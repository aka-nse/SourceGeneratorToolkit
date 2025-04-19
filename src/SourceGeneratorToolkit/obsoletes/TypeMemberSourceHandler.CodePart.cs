using System.Text;

namespace CompilerToolkit.Generators;

public ref partial struct TypeMemberSourceHandler
{
    internal abstract class CodePart : IEquatable<CodePart>
    {
        public abstract bool Equals(CodePart other);

        public abstract void Generate(SourceBuilderState state, StringBuilder sb);
    }


    internal static CodePart LineBreak { get; } = new LineBreakCodePart();
    private sealed class LineBreakCodePart : CodePart
    {
        public override bool Equals(CodePart other)
            => other is LineBreakCodePart;

        public override void Generate(SourceBuilderState state, StringBuilder sb)
            => sb.AppendLine();
    }


    private sealed class LiteralCodePart(string s) : CodePart
    {
        private readonly string _s = s;
        public override bool Equals(CodePart other)
            => other is LiteralCodePart otherLiteral
            && _s == otherLiteral._s;

        public override void Generate(SourceBuilderState state, StringBuilder sb)
            => sb.Append(_s);
    }


    private sealed class TypeSymbolCodePart(ITypeIdentifier typeSymbol) : CodePart
    {
        private readonly ITypeIdentifier _typeSymbol = typeSymbol;

        public override bool Equals(CodePart other)
            => other is TypeSymbolCodePart otherTypeSymbol
            && _typeSymbol.Equals(otherTypeSymbol._typeSymbol);

        public override void Generate(SourceBuilderState state, StringBuilder sb)
            => sb.Append(_typeSymbol.GetDisplayName(state));
    }


    private sealed class FormattedCodePart<T>(T value) : CodePart
    {
        private readonly T _value = value;

        public override bool Equals(CodePart other)
            => other is FormattedCodePart<T> otherFormatted
            && EqualityComparer<T>.Default.Equals(_value, otherFormatted._value);

        public override void Generate(SourceBuilderState state, StringBuilder sb)
            => sb.Append(_value);
    }


}
