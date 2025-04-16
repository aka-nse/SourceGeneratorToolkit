using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CompilerToolkit.Generators
{
    internal interface ITypeSourceBuilder
    {
        public ITypeSourceBuilder AddRawMember(CodePartHandler memberSource);
    }

    internal class SourceBuilder
    {
        private class TypeSourceBuilder : ITypeSourceBuilder
        {
            private readonly INamespaceSymbol? _containingNamespace;
            private readonly INamedTypeSymbol? _containingType;
            private readonly string _typeName;
            private readonly TypeKind _typeKind;
            private readonly bool _isRecord;
            private readonly List<CodePart> _codeParts = [];

            public TypeSourceBuilder(INamedTypeSymbol partialTargetType)
            {
                _containingNamespace = partialTargetType.ContainingNamespace;
                _containingType = partialTargetType.ContainingType;
                _typeName = partialTargetType.Name;
                _typeKind = partialTargetType.TypeKind;
                _isRecord = partialTargetType.IsRecord;
            }

            public TypeSourceBuilder(string fileScopeType, TypeKind typeKind = TypeKind.Class, bool isRecord = false)
            {
                _containingNamespace = null;
                _containingType = null;
                _typeName = fileScopeType;
                _typeKind = typeKind;
                _isRecord = isRecord;
            }

            public ITypeSourceBuilder AddRawMember(CodePartHandler memberSource)
            {
                _codeParts.AddRange(memberSource.CodeParts);
                _codeParts.Add(CodePart.LineBreak);
                return this;
            }

            public void Generate(SourceBuildingState state, StringBuilder builder)
            {
                TypeSymbolLike[] ancestors = [
                    .. getAncestorTypes(_containingType),
                    new(_typeName, _typeKind, _isRecord),
                ];
                GenerateNamespaceInitializer(builder);
                GenerateTypeInitializer(builder, ancestors);
                foreach (var part in _codeParts)
                {
                    part.Generate(state, builder);
                }
                GenerateTypeTerminator(builder, ancestors);
                GenerateNamespaceTerminator(builder);

                static IEnumerable<TypeSymbolLike> getAncestorTypes(INamedTypeSymbol? typeSymbol)
                {
                    if (typeSymbol is null)
                    {
                        yield break;
                    }
                    foreach (var ancestor in getAncestorTypes(typeSymbol.ContainingType))
                    {
                        yield return ancestor;
                    }
                    yield return new TypeSymbolLike(typeSymbol.Name, typeSymbol.TypeKind, typeSymbol.IsRecord);
                }
            }

            private void GenerateNamespaceInitializer(StringBuilder builder)
            {
                if (_containingNamespace is null || _containingNamespace.IsGlobalNamespace)
                {
                    return;
                }
                var namespaceName = _containingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                builder.Append($"namespace {namespaceName} {{ ");
            }

            private void GenerateTypeInitializer(StringBuilder builder, TypeSymbolLike[] ancestorTypes)
            {
                var current = ancestorTypes[0];
                var typeKeyword = GetTypeKeyword(current.TypeKind, current.IsRecord);
                builder.Append($"partial {typeKeyword} {current.Name} {{");
                for (var i = 1; i < ancestorTypes.Length; i++)
                {
                    current = ancestorTypes[i];
                    typeKeyword = GetTypeKeyword(current.TypeKind, current.IsRecord);
                    builder.Append($" partial {typeKeyword} {current.Name} {{");
                }
                builder.AppendLine();
            }

            private void GenerateTypeTerminator(StringBuilder builder, TypeSymbolLike[] ancestorTypes)
                => builder.Append(string.Join(" ", ancestorTypes.Select(_ => "}")));

            private void GenerateNamespaceTerminator(StringBuilder builder)
            {
                if (_containingNamespace is null || _containingNamespace.IsGlobalNamespace)
                {
                    builder.AppendLine();
                }
                else
                {
                    builder.AppendLine("}");
                }
            }

            private static string GetTypeKeyword(TypeKind typeKind, bool isRecord)
            {
                var keyword = typeKind switch
                {
                    TypeKind.Class => "class",
                    TypeKind.Struct => "struct",
                    TypeKind.Interface => "interface",
                    _ => throw new NotSupportedException($"Type kind '{typeKind}' is not supported.")
                };
                return $"{(isRecord ? "record " : "")}{keyword}";
            }

            private record struct TypeSymbolLike(string Name, TypeKind TypeKind, bool IsRecord);
        }


        public SemanticModel SemanticModel { get; }
        public Compilation Compilation { get; }
        public INamedTypeSymbol TypeSymbol { get; }
        public TypeDeclarationSyntax TypeNode { get; }

        private readonly List<TypeSourceBuilder> _typeSourceBuilders = [];

        public static SourceBuilder? Create(SourceProductionContext context, GeneratorAttributeSyntaxContext source, DiagnosticDescriptor? needPartialDescritpror = null)
        {
            var typeNode = (TypeDeclarationSyntax)source.TargetNode;
            if(needPartialDescritpror is { } && !typeNode.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(needPartialDescritpror, typeNode.GetLocation()));
                return null;
            }
            var semanticModel = source.SemanticModel;
            var compilation = semanticModel.Compilation;
            var typeSymbol = (INamedTypeSymbol)source.TargetSymbol;
            return new(semanticModel, compilation, typeSymbol, typeNode);
        }

        private SourceBuilder(
            SemanticModel semanticModel,
            Compilation compilation,
            INamedTypeSymbol typeSymbol,
            TypeDeclarationSyntax typeNode)
        {
            SemanticModel = semanticModel;
            Compilation = compilation;
            TypeSymbol = typeSymbol;
            TypeNode = typeNode;
        }

        public ITypeSourceBuilder CreatePartialType(INamedTypeSymbol targetType)
        {
            var typeSourceBuilder = new TypeSourceBuilder(targetType);
            _typeSourceBuilders.Add(typeSourceBuilder);
            return typeSourceBuilder;
        }

        public ITypeSourceBuilder CreateFileScopeType(string typeName, TypeKind typeKind = TypeKind.Class, bool isRecord = false)
        {
            var typeSourceBuilder = new TypeSourceBuilder(typeName, typeKind, isRecord);
            _typeSourceBuilders.Add(typeSourceBuilder);
            return typeSourceBuilder;
        }

        public string GenerateSource()
        {
            var compilationUnitNode = (CompilationUnitSyntax)TypeNode.SyntaxTree.GetRoot();
            var state = new SourceBuildingState(SemanticModel, compilationUnitNode.Usings.FullSpan.End);
            var sb = new StringBuilder();
            sb.AppendLine(compilationUnitNode.Usings.ToFullString());
            foreach(var type in _typeSourceBuilders)
            {
                type.Generate(state, sb);
            }
            return sb.ToString();
        }
    }

    internal class SourceBuildingState(SemanticModel semanticModel, int nameReferencPsition)
    {
        public SemanticModel SemanticModel { get; } = semanticModel;
        public int NameReferencPsition { get; } = nameReferencPsition;
    }

    [InterpolatedStringHandler]
    internal ref struct CodePartHandler(
#pragma warning disable CS9113
        int literalLength,
#pragma warning restore CS9113
        int formattedCount)
    {
        public IReadOnlyList<CodePart> CodeParts => _codeParts;
        private readonly List<CodePart> _codeParts = new(2 * formattedCount);

        public void AppendLiteral(string value)
            => _codeParts.Add(new CodePart.Literal(value));

        public void AppendFormatted<T>(T value)
            => _codeParts.Add(new CodePart.Formatted<T>(value));

        public void AppendFormatted(INamedTypeSymbol value)
            => _codeParts.Add(new CodePart.FormattedSymbol(value));
    }

    internal abstract class CodePart
    {
        private CodePart() { }

        public abstract void Generate(SourceBuildingState state, StringBuilder builder);

        public sealed class Literal(string value)
            : CodePart
        {
            public override void Generate(SourceBuildingState state, StringBuilder builder)
                => builder.Append(value);
        }

        public static CodePart LineBreak { get; } = new LineBreak_();
        private sealed class LineBreak_ : CodePart
        {
            public override void Generate(SourceBuildingState state, StringBuilder builder)
                 => builder.AppendLine();
        }

        public sealed class Formatted<T>(T value)
            : CodePart
        {
            public override void Generate(SourceBuildingState state, StringBuilder builder)
                => builder.Append(value?.ToString());
        }

        public sealed class FormattedSymbol(INamedTypeSymbol value)
            : CodePart
        {
            public override void Generate(SourceBuildingState state, StringBuilder builder)
            {
                var name = value.ToMinimalDisplayString(
                    state.SemanticModel,
                    state.NameReferencPsition,
                    SymbolDisplayFormat.MinimallyQualifiedFormat);
                builder.Append(name);
            }
        }
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
