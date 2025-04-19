using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using CompilerToolkit.Generators;
using Microsoft.CodeAnalysis;
using static SourceGeneratorToolkit.FrozenSourceBuilder;

namespace SourceGeneratorToolkit
{
    public interface ITypeIdentifier : IEquatable<ITypeIdentifier>
    {
        public INamespaceSymbol? ContainingNamespace { get; }
        public INamedTypeSymbol? ContainingType { get; }
        public string Name { get; }
        public string ToDisplayString();
    }


    public sealed record TypeIdentifier(INamedTypeSymbol Type) : ITypeIdentifier
    {
        public INamespaceSymbol? ContainingNamespace => Type.ContainingNamespace;
        public INamedTypeSymbol? ContainingType => Type.ContainingType;
        public string Name => Type.Name;

        public string ToDisplayString()
            => Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        public bool Equals(ITypeIdentifier other)
            => other is TypeIdentifier otherTypeSymbol
            && SymbolEqualityComparer.Default.Equals(Type, otherTypeSymbol.Type);
    }


    public sealed record FileOnlyTypeIdentifier(string Name) : ITypeIdentifier
    {
        INamespaceSymbol? ITypeIdentifier.ContainingNamespace => null;
        INamedTypeSymbol? ITypeIdentifier.ContainingType => null;

        public string ToDisplayString()
            => Name;

        bool IEquatable<ITypeIdentifier>.Equals(ITypeIdentifier other)
            => other is FileOnlyTypeIdentifier otherFileOnly
            && StringComparer.InvariantCulture.Equals(Name, otherFileOnly.Name);
    }


    public ref struct SourceBuilder()
    {
        internal ref struct BuildState()
        {
            internal ImmutableArray<FrozenSourceBuilder.CodePart>.Builder CodeParts { get; } = ImmutableArray.CreateBuilder<FrozenSourceBuilder.CodePart>();
            internal HashSet<ITypeIdentifier> TypeSymbols { get; } = new();

            public void AddTypeSymbol(INamedTypeSymbol typeSymbol)
            {
                TypeSymbols.Add(new TypeIdentifier(typeSymbol));
            }

            public void AddTypeSymbol(ITypeIdentifier typeSymbol)
            {
                TypeSymbols.Add(typeSymbol);
            }

            public void AddCodePart(FrozenSourceBuilder.CodePart codePart)
            {
                CodeParts.Add(codePart);
            }
        }


        private readonly List<TypeBuilder.Entity> _typeBuilders = [];


        public TypeBuilder CreatePartialType(INamedTypeSymbol typeSymbol)
        {
            var entity = new TypeBuilder.Entity(typeSymbol);
            _typeBuilders.Add(entity);
            return new TypeBuilder(entity);
        }


        public FrozenSourceBuilder Frozen()
        {
            var state = new BuildState();
            foreach (var typeBuilder in _typeBuilders)
            {
                typeBuilder.Frozen(state);
            }
            INamespaceSymbol[] usingNamespaces = [..state
                .TypeSymbols
                .Select(t => t.ContainingNamespace)
                .Distinct(SymbolEqualityComparer.Default)
                .OfType<INamespaceSymbol>()
                .Where(ns => !ns.IsGlobalNamespace)
            ];
            var symbolNameMap = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.InvariantCulture);
            foreach (var typeSymbol in state.TypeSymbols)
            {
                var fullName = typeSymbol.ToDisplayString();
                var name = typeSymbol.Name;
                var uniqueKey = name;
                for(var containing = typeSymbol.ContainingType; containing is not null; containing = containing.ContainingType)
                {
                    uniqueKey = containing.Name;
                    name = $"{containing.Name}.{name}";
                }
                name = isUnique(usingNamespaces, uniqueKey)
                    ? name
                    : fullName;
                symbolNameMap.Add(fullName, name);
            }
            return new FrozenSourceBuilder(
                symbolNameMap.ToImmutable(),
                state.CodeParts.ToImmutable());

            static bool isUnique(INamespaceSymbol[] namespaces, string shortName)
            {
                var count = 0;
                foreach (var ns in namespaces)
                {
                    count += ns.GetMembers(shortName).Count();
                    if (count > 1)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }


    public ref struct TypeBuilder
    {
        internal abstract class CodePart
        {
            public abstract void Frozen(SourceBuilder.BuildState state);
        }

        internal static CodePart LineBreak { get; } = new LineBreakCodePart();

        private sealed class LineBreakCodePart : CodePart
        {
            public override void Frozen(SourceBuilder.BuildState state)
                => state.AddCodePart(FrozenSourceBuilder.LineBreak);
        }

        private sealed class LiteralCodePart(string s) : CodePart
        {
            public override void Frozen(SourceBuilder.BuildState state)
                => state.AddCodePart(new FrozenSourceBuilder.LiteralCodePart(s));
        }

        private sealed class TypeSymbolCodePart(INamedTypeSymbol typeSymbol) : CodePart
        {
            public override void Frozen(SourceBuilder.BuildState state)
            {
                state.AddTypeSymbol(typeSymbol);
                state.AddCodePart(new FrozenSourceBuilder.TypeSymbolCodePart(typeSymbol.ToDisplayString()));
            }
        }

        private sealed class FormattedCodePart<T>(T value) : CodePart
        {
            public override void Frozen(SourceBuilder.BuildState state)
                => state.AddCodePart(new FrozenSourceBuilder.FormattedCodePart<T>(value));
        }


        [InterpolatedStringHandler]
        public ref struct TypeMemberSourceHandler(int literalLength, int formattedCount)
        {
            public readonly int LiteralLength => literalLength;

            internal readonly IReadOnlyList<CodePart> CodeParts => _codeParts ?? [];
            private readonly List<CodePart> _codeParts = new(formattedCount * 2 + 1);

            public void AppendLiteral(string s)
                => _codeParts.Add(new LiteralCodePart(s));

            public void AppendFormatted(INamedTypeSymbol typeSymbol)
                => _codeParts.Add(new TypeSymbolCodePart(typeSymbol));

            public void AppendFormatted<T>(T value)
            {
                CodePart part = value is INamedTypeSymbol typeSymbol
                    ? new TypeSymbolCodePart(typeSymbol)
                    : new FormattedCodePart<T>(value);
                _codeParts.Add(part);
            }
        }


        internal class Entity(
            INamespaceSymbol? containingNamespace,
            INamedTypeSymbol? containingType,
            string name,
            ImmutableArray<string> typeArgs,
            TypeKind typeKind,
            bool isRecord)
        {
            private readonly List<CodePart> _codeParts = [];

            public Entity(INamedTypeSymbol type)
                : this(
                      type.ContainingNamespace,
                      type.ContainingType,
                      type.Name,
                      type.TypeArguments.Select(ta => ta.Name).ToImmutableArray(),
                      type.TypeKind,
                      type.IsRecord)
            {
            }

            public void AddRawMember(TypeMemberSourceHandler source)
            {
                _codeParts.AddRange(source.CodeParts);
            }

            public void Frozen(SourceBuilder.BuildState state)
            {
                foreach (var part in _codeParts)
                {
                    part.Frozen(state);
                }
            }
        }


        private readonly Entity _entity;

        internal TypeBuilder(Entity entity)
        {
            _entity = entity;
        }
    }




    public sealed class FrozenSourceBuilder(
        ImmutableDictionary<string, string> symbolNameMap,
        ImmutableArray<FrozenSourceBuilder.CodePart> codeParts
        ) : IEquatable<FrozenSourceBuilder>
    {
        public abstract class CodePart : IEquatable<CodePart>
        {
            public abstract bool Equals(CodePart other);
            public abstract void Generate(FrozenSourceBuilder owner, StringBuilder sb);
        }

        internal static CodePart LineBreak { get; } = new LineBreakCodePart();
        private sealed class LineBreakCodePart : CodePart
        {
            public override bool Equals(CodePart other)
                => other is LineBreakCodePart;
            public override void Generate(FrozenSourceBuilder owner, StringBuilder sb)
                => sb.AppendLine();
        }

        internal sealed class LiteralCodePart(string s) : CodePart
        {
            private readonly string _s = s;

            public override bool Equals(CodePart other)
                => other is LiteralCodePart otherLiteral
                && _s == otherLiteral._s;

            public override void Generate(FrozenSourceBuilder owner, StringBuilder sb)
                => sb.Append(_s);
        }

        internal sealed class TypeSymbolCodePart(string fullName) : CodePart
        {
            private readonly string _fullName = fullName;

            public override bool Equals(CodePart other)
                => other is TypeSymbolCodePart otherTypeSymbol
                && StringComparer.InvariantCulture.Equals(_fullName, otherTypeSymbol._fullName);

            public override void Generate(FrozenSourceBuilder owner, StringBuilder sb)
                => sb.Append(owner._symbolNameMap[_fullName]);
        }

        internal sealed class FormattedCodePart<T>(T value) : CodePart
        {
            private readonly T _value = value;

            public override bool Equals(CodePart other)
                => other is FormattedCodePart<T> otherFormatted
                && EqualityComparer<T>.Default.Equals(_value, otherFormatted._value);

            public override void Generate(FrozenSourceBuilder owner, StringBuilder sb)
                => sb.Append(_value);
        }


        private readonly ImmutableDictionary<string, string> _symbolNameMap = symbolNameMap;
        private readonly ImmutableArray<CodePart> _codeParts = codeParts;

        public string Generate()
        {
            var sb = new StringBuilder();
            foreach (CodePart part in _codeParts)
            {
                part.Generate(this, sb);
            }
            return sb.ToString();
        }

        public bool Equals(FrozenSourceBuilder other)
        {
            if (other is null)
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (_codeParts.Length != other._codeParts.Length)
            {
                return false;
            }
            return _symbolNameMap.SequenceEqual(other._symbolNameMap);
        }
    }
}
