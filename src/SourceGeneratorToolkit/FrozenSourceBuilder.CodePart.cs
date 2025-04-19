using System.Runtime.CompilerServices;
using System.Text;

namespace SourceGeneratorToolkit;

partial class FrozenSourceBuilder
{
    public readonly struct CodePart : IEquatable<CodePart>
    {
        private enum Kind
        {
            Literal,
            TypeSymbol,
        }

        private readonly Kind _kind;
        private readonly bool _linebreak;
        private readonly ReadOnlyMemory<char> _text;

        private CodePart(Kind kind, ReadOnlyMemory<char> text, bool linebreak)
        {
            _kind = kind;
            _linebreak = linebreak;
            _text = text;
        }

        private CodePart(Kind kind, string text, bool linebreak)
            : this(kind, text.AsMemory(), linebreak)
        {
        }

        public bool Equals(CodePart other)
            => _kind == other._kind
            && MemoryExtensions.Equals(_text.Span, other._text.Span, StringComparison.InvariantCulture);

        public void Generate(BuilderState state, StringBuilder sb)
        {
            switch(_kind)
            {
            case Kind.Literal:
                sb.Append(_text.Span);
                break;
            case Kind.TypeSymbol:
                sb.Append(state.GetDisplayName(_text));
                break;
            default:
                throw new InvalidOperationException();
            }
            if (_linebreak)
            {
                sb.AppendLine();
            }
        }


        public static CodePart Linebreak { get; }
            = Literal(ReadOnlyMemory<char>.Empty, true);

        public static CodePart StartAttribute { get; }
            = Literal("[", false);

        public static CodePart EndAttribute { get; }
            = Literal("[", true);

        public static CodePart StartScope { get; }
            = Literal("{", true);

        public static CodePart EndScope { get; }
            = Literal("}", true);

        public static CodePart Literal(string s, bool linebreak)
            => Literal(s.AsMemory(), linebreak);

        public static CodePart Literal(ReadOnlyMemory<char> s, bool linebreak)
        {
            if (s.IsEmpty && linebreak)
            {
                return Linebreak;
            }
            return new CodePart(Kind.Literal, s, linebreak);
        }

        public static CodePart TypeSymbol(string fullName)
            => new (Kind.TypeSymbol, fullName, false);

        public static CodePart Formatted<T>(T value)
            => Literal(value?.ToString() ?? "", false);
    }

}

