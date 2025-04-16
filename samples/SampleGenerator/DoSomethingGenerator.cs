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
        if (SourceBuilder.Create(context, source) is not { } builder)
        {
            return;
        }
        var typeBuilder = builder.CreatePartialType(builder.TypeSymbol);
        typeBuilder.AddRawMember($$"""
                public string HelloWorld() => "Hello, World!";

            """);
        var sourceCode = builder.GenerateSource();
        context.AddSource(
            $"{nameof(DoSomethingGenerator)}-{builder.TypeSymbol.Name}.g.cs",
            sourceCode);
    }



    private const string AttributeSource = """
        using System;
        namespace SampleGeneratorGenerated;

        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
        internal sealed class DoSomethingAttribute : Attribute
        {
        }
        """;
}
