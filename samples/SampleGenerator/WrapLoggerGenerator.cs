using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using SourceGeneratorToolkit;

namespace SampleGenerator;

[Generator(LanguageNames.CSharp)]
public class WrapLoggerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            context.AddSource("WrapLoggerAttribute.cs", _attributeSource);
        });

        var source = context.SyntaxProvider.ForAttributeWithMetadataName(
            "SampleGeneratorGenerated.WrapLoggerAttribute",
            static (node, token) => true,
            static (context, token) => context);
        context.RegisterSourceOutput(source, Emit);
    }


    private static void Emit(
        SourceProductionContext context,
        GeneratorAttributeSyntaxContext source)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var builder = new SourceBuilder(source);
        builder.Append($$"""
            /*
            hello, world!
            */

            """);


        context.CancellationToken.ThrowIfCancellationRequested();
        using (var type = builder.BeginTargetTypeDeclare())
        {
            builder.Append($$"""
                public string SayHello()
                    => "Hello, world!";
                """);
        }

        context.CancellationToken.ThrowIfCancellationRequested();
        var hintName = builder.GetPreferHintName(prefix: "", suffix: "");
        var sourceCode = builder.Build();
        context.AddSource(hintName, sourceCode);
    }


    private const string _attributeSource = """
        using System;
        namespace SampleGeneratorGenerated;

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
        internal sealed class WrapLoggerAttribute : Attribute
        {
        }
        """;
}


file static class Helpers
{
    public static string ToFullNameString(this INamedTypeSymbol? type)
        => type is null
            ? ""
            : type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public static string ToFullNameString(this INamespaceSymbol? @namespace)
        => @namespace is null || @namespace.IsGlobalNamespace
            ? ""
            : @namespace.ToDisplayString();
}
