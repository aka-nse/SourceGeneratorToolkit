using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CompilerToolkit.Generators
{
    internal sealed record TypeSymbolInfo(
        string? ContainingNamespace,
        TypeSymbolInfo? ContainingType,
        string Name,
        TypeKind TypeKind,
        bool IsRecord,
        bool IsFileOnly)
        : IEquatable<TypeSymbolInfo>
    {
        public TypeSymbolInfo(INamedTypeSymbol symbol)
            : this(
                symbol.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : symbol.ContainingNamespace.ToDisplayString(),
                symbol.ContainingType is INamedTypeSymbol containingType
                    ? new TypeSymbolInfo(containingType)
                    : null,
                symbol.Name,
                symbol.TypeKind,
                symbol.IsRecord,
                true)
        {
        }

        public string ToFullString()
        {
            if(ContainingType is { })
            {
                return $"{ContainingType.ToFullString()}.{Name}";
            }
            else
            {
                var ns = string.IsNullOrEmpty(ContainingNamespace)
                    ? $"{ContainingNamespace}."
                    : "";
                return $"{ns}{Name}";
            }
        }

        public static IEnumerable<TypeSymbolInfo> Ancestors(TypeSymbolInfo type)
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
    }


    internal class SourceBuilder(GeneratorAttributeSyntaxContext context)
        : IEquatable<SourceBuilder>
    {
        private sealed record class TypeBuilder(TypeSymbolInfo TargetType)
            : ITypeBuilder, IEquatable<TypeBuilder>
        {
            private sealed record class AttributeInfo(
                TypeSymbolInfo AttributeType,
                IReadOnlyList<TypeMemberSourceHandler.CodePart> Arguments)
            {
                public bool Equals(AttributeInfo other)
                {
                    if(other is null)
                    {
                        return false;
                    }
                    if (!AttributeType.Equals(other.AttributeType))
                    {
                        return false;
                    }
                    if (!Arguments.SequenceEqual(other.Arguments))
                    {
                        return false;
                    }
                    return true;
                }

                public override int GetHashCode()
                    => AttributeType.GetHashCode() ^ Arguments.Count;
            }

            private sealed record class TypeArgumentInfo(
                string Name,
                IReadOnlyList<TypeMemberSourceHandler.CodePart> CodeParts)
            {
                public bool Equals(TypeArgumentInfo other)
                {
                    if (other is null)
                    {
                        return false;
                    }
                    if (Name != other.Name)
                    {
                        return false;
                    }
                    if (!CodeParts.SequenceEqual(other.CodeParts))
                    {
                        return false;
                    }
                    return true;
                }

                public override int GetHashCode()
                    => Name.GetHashCode() ^ CodeParts.Count;
            }


            private readonly List<AttributeInfo> _attributes = [];

            private readonly List<TypeArgumentInfo> _typeArguments = [];

            private readonly List<TypeMemberSourceHandler.CodePart> _memberParts = [];

            public bool Equals(TypeBuilder other)
            {
                if(other is null)
                {
                    return false;
                }
                if(!TargetType.Equals(other.TargetType))
                {
                    return false;
                }
                if (!_attributes.SequenceEqual(other._attributes))
                {
                    return false;
                }
                if (!_typeArguments.SequenceEqual(other._typeArguments))
                {
                    return false;
                }
                if (!_memberParts.SequenceEqual(other._memberParts))
                {
                    return false;
                }
                return true;
            }

            public override int GetHashCode()
                => TargetType.GetHashCode() ^ _attributes.Count ^ _typeArguments.Count ^ _memberParts.Count;

            public void PreGenerate(SourceBuilderState.Builder state)
            {
                state.AppendTypeUse(TargetType);
                foreach (var attribute in _attributes)
                {
                    state.AppendTypeUse(attribute.AttributeType);
                    foreach (var arg in attribute.Arguments)
                    {
                        arg.PreGenerate(state);
                    }
                }
                foreach (var typeArg in _typeArguments)
                {
                    foreach (var arg in typeArg.CodeParts)
                    {
                        arg.PreGenerate(state);
                    }
                }
                foreach (var member in _memberParts)
                {
                    member.PreGenerate(state);
                }
            }

            public void Generate(SourceBuilderState state, StringBuilder sb)
            {
                var ancestors = TypeSymbolInfo.Ancestors(TargetType);
                sb.AppendJoin(" ", ancestors.Select(ancestor =>
                {
                    var keyword = Keyword(ancestor.TypeKind, ancestor.IsRecord);
                    return $"partial {keyword} {ancestor.Name} {{";
                }));
                sb.AppendLine();
                foreach (var attr in _attributes)
                {
                    sb.Append($"[{state.GetDisplayName(attr.AttributeType)}(");
                    foreach(var part in attr.Arguments)
                    {
                        part.Generate(state, sb);
                    }
                    sb.AppendLine(")]");
                }
                sb.Append(TargetType.IsFileOnly ? "file " : "partial ");
                sb.Append(Keyword(TargetType.TypeKind, TargetType.IsRecord));
                sb.AppendLine($" {TargetType.Name}");
                sb.AppendLine("{");

                foreach (var memberPart in _memberParts)
                {
                    memberPart.Generate(state, sb);
                }
                sb.AppendLine("}");
                sb.AppendJoin(" ", ancestors.Select(_ => "}"));
                sb.AppendLine();
            }

            ITypeBuilder ITypeBuilder.AddAttribute(TypeSymbolInfo attributeType, TypeMemberSourceHandler arguments)
            {
                _attributes.Add(new(attributeType, arguments.CodeParts));
                return this;
            }

            ITypeBuilder ITypeBuilder.AddGenericTypeArgument(string name, TypeMemberSourceHandler restriction)
            {
                _typeArguments.Add(new(name, restriction.CodeParts));
                return this;
            }

            ITypeBuilder ITypeBuilder.AddRawMember(TypeMemberSourceHandler source)
            {
                _memberParts.AddRange(source.CodeParts);
                _memberParts.Add(TypeMemberSourceHandler.LineBreak);
                return this;
            }

            private static string Keyword(TypeKind typeKind, bool IsRecord)
                => (IsRecord ? "record " : "")
                + (typeKind switch
                {
                    TypeKind.Class => "class",
                    TypeKind.Struct => "struct",
                    TypeKind.Interface => "interface",
                    _ => throw new NotSupportedException(),
                });
        }


        private readonly string _targetNamespace
            = context.TargetSymbol.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        private readonly List<TypeBuilder> _definedTypes = [];

        public bool Equals(SourceBuilder other)
            => Equals(this, other);

        public static bool Equals(SourceBuilder? x, SourceBuilder? y)
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
            throw new NotImplementedException();
        }

        public ITypeBuilder CreateType(TypeSymbolInfo type)
        {
            var builder = new TypeBuilder(type);
            _definedTypes.Add(builder);
            return builder;
        }

        public ITypeBuilder CreatePartialType(INamedTypeSymbol type)
            => CreateType(new (type));

        public ITypeBuilder CreateFileOnlyType(string name, TypeKind typeKind = TypeKind.Class, bool isRecord = false)
            => CreateType(new (null, null, name, typeKind, isRecord, true));

        public string Build()
        {
            var stateBuilder = new SourceBuilderState.Builder(new HashSet<TypeSymbolInfo>());
            foreach (var type in _definedTypes)
            {
                type.PreGenerate(stateBuilder);
            }
            var state = stateBuilder.Build();

            var sb = new StringBuilder();
            foreach(var ns in state.GetUsingNamespaces(_targetNamespace))
            {
                sb.AppendLine($"using {ns};");
            }

            foreach (var type in _definedTypes)
            {
                type.Generate(state, sb);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }


    internal interface ITypeBuilder
    {
        TypeSymbolInfo TargetType { get; }
        ITypeBuilder AddGenericTypeArgument(string name, TypeMemberSourceHandler restriction = default);
        ITypeBuilder AddAttribute(TypeSymbolInfo attributeType, TypeMemberSourceHandler arguments = default);
        ITypeBuilder AddRawMember(TypeMemberSourceHandler source);
    }


    internal class SourceBuilderState
    {
        public ref struct Builder(HashSet<TypeSymbolInfo> typeSymbols)
        {
            public void AppendTypeUse(TypeSymbolInfo typeSymbol)
            {
                typeSymbols.Add(typeSymbol);
            }

            public SourceBuilderState Build()
            {
                var namespaces = new HashSet<string>();
                var reverseLookup = new Dictionary<string, List<TypeSymbolInfo>>();
                foreach (var type in typeSymbols)
                {
                    if(type.ContainingNamespace is { } ns)
                    {
                        namespaces.Add(ns);
                    }

                    if (reverseLookup.TryGetValue(type.Name, out var list))
                    {
                        list.Add(type);
                    }
                    else
                    {
                        reverseLookup[type.Name] = new List<TypeSymbolInfo> { type };
                    }
                }
                var displayNames = new Dictionary<TypeSymbolInfo, string>();
                foreach(var kv in reverseLookup)
                {
                    if(kv.Value.Count == 1)
                    {
                        displayNames[kv.Value[0]] = kv.Key;
                    }
                    else
                    {
                        foreach (var type in kv.Value)
                        {
                            displayNames[type] = type.ToFullString();
                        }
                    }
                }
                return new SourceBuilderState(namespaces, displayNames);
            }
        }

        private HashSet<string> Namespaces { get; }
        private Dictionary<TypeSymbolInfo, string> DisplayNames { get; }

        private SourceBuilderState(
            HashSet<string> namespaces,
            Dictionary<TypeSymbolInfo, string> displayNames)
        {
            Namespaces = namespaces;
            DisplayNames = displayNames;
        }

        public IEnumerable<string> GetUsingNamespaces(string fileScopeNamespace)
            => Namespaces.Where(x => x != fileScopeNamespace);

        public string GetDisplayName(TypeSymbolInfo type)
            => DisplayNames[type];
    }


    [InterpolatedStringHandler]
    internal ref struct TypeMemberSourceHandler(int literalLength, int formattedCount)
    {
        public abstract class CodePart : IEquatable<CodePart>
        {
            public abstract bool Equals(CodePart other);

            public virtual void PreGenerate(SourceBuilderState.Builder state) { }
            public abstract void Generate(SourceBuilderState state, StringBuilder sb);
        }

        public static CodePart LineBreak { get; } = new LineBreakCodePart();
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

        private sealed class TypeSymbolCodePart(TypeSymbolInfo typeSymbol) : CodePart
        {
            private readonly TypeSymbolInfo _typeSymbol = typeSymbol;

            public override bool Equals(CodePart other)
                => other is TypeSymbolCodePart otherTypeSymbol
                && _typeSymbol.Equals(otherTypeSymbol._typeSymbol);

            public override void PreGenerate(SourceBuilderState.Builder state)
                => state.AppendTypeUse(_typeSymbol);

            public override void Generate(SourceBuilderState state, StringBuilder sb)
                => sb.Append(state.GetDisplayName(_typeSymbol));
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

        public readonly int LiteralLength => literalLength;

        public readonly IReadOnlyList<CodePart> CodeParts => _codeParts ?? [];
        private readonly List<CodePart> _codeParts = new (formattedCount * 2 + 1);

        public void AppendLiteral(string s)
            => _codeParts.Add(new LiteralCodePart(s));

        public void AppendFormatted(TypeSymbolInfo typeSymbol)
            => _codeParts.Add(new TypeSymbolCodePart(typeSymbol));

        public void AppendFormatted<T>(T value)
            => _codeParts.Add(new FormattedCodePart<T>(value));
    }
}


namespace System.Runtime.CompilerServices
{
#if !NET6_0_OR_GREATER
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class InterpolatedStringHandlerAttribute : Attribute
    {
    }
#endif

#if !NET5_0_OR_GREATER
    internal static class IsExternalInit;
#endif
}

file static class Helpers
{
    public static void AppendJoin(this StringBuilder sb, string separator, IEnumerable<string> values)
    {
        var iter = values.GetEnumerator();
        if (!iter.MoveNext())
        {
            return;
        }
        sb.Append(iter.Current);
        while (iter.MoveNext())
        {
            sb.Append(separator);
            sb.Append(iter.Current);
        }
    }
}
