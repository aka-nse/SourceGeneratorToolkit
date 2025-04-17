using System;
using System.Collections.Generic;
using System.Text;
using CompilerToolkit.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SampleGenerator;

[Generator(LanguageNames.CSharp)]
public class DoSomethingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            context.AddSource("DoSomethingAttribute.cs", AttributeSource);
        });
        var source = context.SyntaxProvider.ForAttributeWithMetadataName(
            "SampleGeneratorGenerated.DoSomethingAttribute",
            static (node, token) => true,
            static (context, token) => context);
        context.RegisterSourceOutput(source, Emit);
    }


    private static void Emit(SourceProductionContext context, GeneratorAttributeSyntaxContext source)
    {
        var builder = new SourceBuilder(source);
        var typeBuilder = builder.CreatePartialType((INamedTypeSymbol)source.TargetSymbol);
        typeBuilder.AddRawMember($$"""
                public string HelloWorld() => "Hello, World!";

            """);
        var sourceCode = builder.Build();
        context.AddSource(
            $"{nameof(DoSomethingGenerator)}-{typeBuilder.TargetType.Name}.g.cs",
            sourceCode);
    }



    private const string AttributeSource = """
        using System;
        namespace SampleGeneratorGenerated;

        // [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
        internal sealed class DoSomethingAttribute : Attribute
        {
        }
        """;
}
