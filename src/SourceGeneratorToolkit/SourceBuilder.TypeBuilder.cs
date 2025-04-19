using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Xml.Linq;
using FrozenSB = SourceGeneratorToolkit.FrozenSourceBuilder;

namespace SourceGeneratorToolkit;

partial struct SourceBuilder
{
    public readonly ref struct TypeBuilder
    {
        private readonly TypeBuilderEntity _entity;

        internal TypeBuilder(TypeBuilderEntity entity)
        {
            _entity = entity;
        }

        public TypeBuilder AddAttribute(SourceStringHandler handler)
        {
            _entity.AddAttribute(handler);
            return this;
        }

        public TypeBuilder AddGenericConstraint(SourceStringHandler handler)
        {
            _entity.AddGenericConstraint(handler);
            return this;
        }

        public TypeBuilder AddRawMember(SourceStringHandler handler)
        {
            _entity.AddRawMember(handler);
            return this;
        }
    }

    internal abstract class TypeBuilderEntity
    {
        private readonly List<CodePart> _attributes = [];
        private readonly List<CodePart> _genericTypeConstraints = [];
        private readonly List<CodePart> _memberCodeParts = [];

        private protected TypeBuilderEntity() { }

        public void AddAttribute(SourceStringHandler handler)
        {
            _attributes.AddRange(handler.CodeParts);
        }

        public void AddGenericConstraint(SourceStringHandler handler)
        {
            _genericTypeConstraints.AddRange(handler.CodeParts);
        }

        public void AddRawMember(SourceStringHandler handler)
        {
            _memberCodeParts.AddRange(handler.CodeParts);
            _memberCodeParts.Add(CodePart.Linebreak);
        }

        public void Frozen(FrozenState state)
        {
            FrozenContainingTypeStart(state);
            foreach (var attribute in _attributes)
            {
                state.AddCodePart(FrozenSB.CodePart.StartAttribute);
                attribute.Frozen(state);
                state.AddCodePart(FrozenSB.CodePart.EndAttribute);
            }
            FrozenTypeDeclaration(state);
            foreach (var genericTypeConstraint in _genericTypeConstraints)
            {
                genericTypeConstraint.Frozen(state);
            }
            state.AddCodePart(FrozenSB.CodePart.StartScope);
            foreach (var memberCodePart in _memberCodeParts)
            {
                memberCodePart.Frozen(state);
            }
            state.AddCodePart(FrozenSB.CodePart.EndScope);
            FrozenContainingTypeEnd(state);
        }

        /// <remarks>
        /// <para>Contain tail linebreak. Contain tail begin-bracket.</para>
        /// example:<code><![CDATA[
        /// partial class Foo<T> { partial struct Bar { partial interface IBaz {
        /// ]]></code>
        /// </remarks>
        protected abstract void FrozenContainingTypeStart(FrozenState state);

        /// <remarks>
        /// <para>Contain tail linebreak. Do not contain tail begin-bracket.</para>
        /// example:<code><![CDATA[
        /// partial class Foo<T1, T2, T3>
        /// ]]></code>
        /// </remarks>
        protected abstract void FrozenTypeDeclaration(FrozenState state);

        /// <remarks>
        /// <para>Do not contain end-bracket for target class</para>
        /// example:<code><![CDATA[
        /// } } }</code>
        /// </remarks>
        protected abstract void FrozenContainingTypeEnd(FrozenState state);

        protected static FrozenSB.CodePart Keyword(TypeKind typeKind, bool isRecord)
            => (typeKind, isRecord) switch
            {
                (TypeKind.Class, false) => Class,
                (TypeKind.Class, true) => ClassRecord,
                (TypeKind.Struct, false) => Struct,
                (TypeKind.Struct, true) => StructRecord,
                (TypeKind.Interface, _) => Interface,
                _ => throw new NotSupportedException($"({typeKind}, isRecord={isRecord})"),
            };
        protected static readonly FrozenSB.CodePart Space = FrozenSB.CodePart.Literal(" ", false);
        protected static readonly FrozenSB.CodePart TypeArgsStart = FrozenSB.CodePart.Literal("<", false);
        protected static readonly FrozenSB.CodePart TypeArgsEnd = FrozenSB.CodePart.Literal(">", false);
        protected static readonly FrozenSB.CodePart Comma = FrozenSB.CodePart.Literal(", ", false);
        protected static readonly FrozenSB.CodePart Class = FrozenSB.CodePart.Literal("partial class ", false);
        protected static readonly FrozenSB.CodePart Struct = FrozenSB.CodePart.Literal("partial struct ", false);
        protected static readonly FrozenSB.CodePart Interface = FrozenSB.CodePart.Literal("partial interface ", false);
        protected static readonly FrozenSB.CodePart ClassRecord = FrozenSB.CodePart.Literal("partial class ", false);
        protected static readonly FrozenSB.CodePart StructRecord = FrozenSB.CodePart.Literal("partial struct ", false);
    }


    private sealed class SymbolTypeBuilderEntity(INamedTypeSymbol type) : TypeBuilderEntity
    {
        private static IEnumerable<INamedTypeSymbol> GetContainingTree(INamedTypeSymbol? containingType)
        {
            if(containingType is null)
            {
                yield break;
            }
            foreach(var x in GetContainingTree(containingType.ContainingType))
            {
                yield return x;
            }
            yield return containingType;
        }
        private readonly INamedTypeSymbol[] _containingTree = [.. GetContainingTree(type.ContainingType)];

        private static void FrozenTypeDeclaration(FrozenState state, INamedTypeSymbol type)
        {
            state.AddCodePart(Keyword(type.TypeKind, type.IsRecord));
            state.AddCodePart(FrozenSB.CodePart.Literal(type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), false));
            if (type.TypeArguments.Length > 0)
            {
                state.AddCodePart(TypeArgsStart);
                state.AddCodePart(FrozenSB.CodePart.Literal(type.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), false));
                for (int i = 1; i < type.TypeArguments.Length; i++)
                {
                    state.AddCodePart(Comma);
                    state.AddCodePart(FrozenSB.CodePart.Literal(type.TypeArguments[i].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), false));
                }
                state.AddCodePart(TypeArgsEnd);
            }
        }

        protected override void FrozenContainingTypeStart(FrozenState state)
        {
            if(_containingTree.Length == 0)
            {
                return;
            }
            FrozenTypeDeclaration(state, _containingTree[0]);
            for(var i = 1; i < _containingTree.Length; i++)
            {
                state.AddCodePart(Space);
                FrozenTypeDeclaration(state, _containingTree[i]);
            }
            state.AddCodePart(FrozenSB.CodePart.Linebreak);
        }

        protected override void FrozenTypeDeclaration(FrozenState state)
        {
            FrozenTypeDeclaration(state, type);
            state.AddCodePart(FrozenSB.CodePart.Linebreak);
        }

        protected override void FrozenContainingTypeEnd(FrozenState state)
        {
            if (_containingTree.Length == 0)
            {
                return;
            }
            state.AddCodePart(FrozenSB.CodePart.EndScope);
            for (var i = 1; i < _containingTree.Length; i++)
            {
                state.AddCodePart(Space);
                state.AddCodePart(FrozenSB.CodePart.EndScope);
            }
            state.AddCodePart(FrozenSB.CodePart.Linebreak);
        }
    }


    private sealed class FileOnlyTypeBuilderEntity(
        string name,
        ImmutableArray<string> typeArguments = default,
        TypeKind typeKind = TypeKind.Class,
        bool isRecord = false) : TypeBuilderEntity
    {
        protected override void FrozenContainingTypeStart(FrozenState state)
        {
        }

        protected override void FrozenTypeDeclaration(FrozenState state)
        {
            state.AddCodePart(Keyword(typeKind, isRecord));
            state.AddCodePart(FrozenSB.CodePart.Literal(name, false));
            if (!typeArguments.IsDefaultOrEmpty)
            {
                state.AddCodePart(TypeArgsStart);
                state.AddCodePart(FrozenSB.CodePart.Literal(typeArguments[0], false));
                for (int i = 1; i < typeArguments.Length; i++)
                {
                    state.AddCodePart(Comma);
                    state.AddCodePart(FrozenSB.CodePart.Literal(typeArguments[i], false));
                }
                state.AddCodePart(TypeArgsEnd);
            }
        }

        protected override void FrozenContainingTypeEnd(FrozenState state)
        {
        }
    }


    [InterpolatedStringHandler]
    public readonly ref struct SourceStringHandler(int literalLength, int formattedCount)
    {
        public readonly int LiteralLength => literalLength;

        internal readonly IReadOnlyList<CodePart> CodeParts => _codeParts ?? [];
        private readonly List<CodePart> _codeParts = new(formattedCount * 2 + 1);

        public void AppendLiteral(string s)
        {
            if(s == "\n")
            {
                _codeParts.Add(CodePart.Linebreak);
                return;
            }

            var start = 0;
            var lf = s.IndexOf('\n');
            while(lf > 0)
            {
                var end = s[lf - 1] == '\r'
                    ? lf - 1
                    : lf;
                _codeParts.Add(CodePart.Literal(s.AsMemory(start, end - start), true));
                start = lf + 1;
            }
            if (start < s.Length)
            {
                _codeParts.Add(CodePart.Literal(s.AsMemory(start), false));
            }
        }

        public void AppendFormatted(INamedTypeSymbol typeSymbol)
            => _codeParts.Add(CodePart.TypeSymbol(typeSymbol));

        public void AppendFormatted<T>(T value)
        {
            CodePart part = value is INamedTypeSymbol typeSymbol
                ? CodePart.TypeSymbol(typeSymbol)
                : CodePart.Formatted<T>(value);
            _codeParts.Add(part);
        }
    }
}
