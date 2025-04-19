using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGeneratorToolkit;
using FrozenSB = SourceGeneratorToolkit.FrozenSourceBuilder;

namespace SourceGeneratorToolkit;

public readonly ref partial struct SourceBuilder(GeneratorAttributeSyntaxContext context)
{
    private readonly List<TypeBuilderEntity> _typeBuilders = [];

    public TypeBuilder CreatePartialType(INamedTypeSymbol targetType)
    {
        var entity = new SymbolTypeBuilderEntity(targetType);
        _typeBuilders.Add(entity);
        return new TypeBuilder(entity);
    }


    public TypeBuilder CreateFileOnlyType(
        string name,
        ImmutableArray<string> typeArguments = default,
        TypeKind typeKind = TypeKind.Class,
        bool isRecord = false)
    {
        var entity = new FileOnlyTypeBuilderEntity(name, typeArguments, typeKind, isRecord);
        _typeBuilders.Add(entity);
        return new TypeBuilder(entity);
    }


    public FrozenSB Frozen()
    {
        var state = new FrozenState();
        foreach (var typeBuilder in _typeBuilders)
        {
            typeBuilder.Frozen(state);
        }

        var usings = context
            .TargetNode
            .Ancestors()
            .OfType<CompilationUnitSyntax>()
            .First()
            .ChildNodes()
            .OfType<UsingDirectiveSyntax>()
            .ToArray();
        var nsDecls = context
            .TargetNode
            .Ancestors()
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();
        var referencePosition = nsDecls?.FullSpan.End
            ?? usings.LastOrDefault()?.FullSpan.End
            ?? 0;

        var codeParts = ImmutableArray.CreateBuilder<FrozenSB.CodePart>(usings.Length + 2 + state._codeParts.Count);
        foreach (var @using in usings)
        {
            codeParts.Add(FrozenSB.CodePart.Literal(@using.ToFullString(), true));
        }
        if (nsDecls is not null)
        {
            codeParts.Add(FrozenSB.CodePart.Literal($"namespace {nsDecls.Name};", true));
        }
        codeParts.Add(FrozenSB.CodePart.Linebreak);
        codeParts.AddRange(state._codeParts);

        var nameMap = ImmutableDictionary.CreateBuilder<ReadOnlyMemory<char>, string>(StringViewComparer.Default);
        foreach (var type in state._referenceTypes)
        {
            var fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var displayName = type.ToMinimalDisplayString(
                context.SemanticModel,
                referencePosition,
                SymbolDisplayFormat.MinimallyQualifiedFormat);
            nameMap.Add(fullName.AsMemory(), displayName);
        }
        return new(
            codeParts.ToImmutable(),
            nameMap.ToImmutable());
    }


    internal readonly ref struct FrozenState()
    {
        internal readonly HashSet<INamedTypeSymbol> _referenceTypes
            = new(SymbolEqualityComparer.Default);

        internal readonly List<FrozenSB.CodePart> _codeParts = [];

        public void AddType(INamedTypeSymbol type)
            => _referenceTypes.Add(type);

        public void AddCodePart(FrozenSB.CodePart codePart)
            => _codeParts.Add(codePart);
    }

}
