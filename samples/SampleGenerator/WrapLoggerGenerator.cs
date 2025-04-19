using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGeneratorToolkit;

namespace SampleGenerator;

[Generator(LanguageNames.CSharp)]
public class WrapLoggerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            context.AddSource("WrapLoggerAttribute.cs", AttributeSource);
        });
        var source = context.SyntaxProvider.ForAttributeWithMetadataName(
            "SampleGeneratorGenerated.WrapLoggerAttribute",
            static (node, token) => true,
            static (context, token) =>
            {
                // return 0;
                var sb = new SourceBuilder(context);
                var type = sb.CreatePartialType((INamedTypeSymbol)context.TargetSymbol);
                type.AddRawMember($$"""
                    public string Hello() => "Hello, world!";
                    """);
                var ft = sb.CreateFileOnlyType("Helper");
                return sb.Frozen();
            });
        context.RegisterSourceOutput(source, Emit);
    }

    private static void Emit(SourceProductionContext context, int source) { }

    private static void Emit(SourceProductionContext context, FrozenSourceBuilder source)
    {
        var s = source.Build();
        Console.WriteLine(s);
    }


    private const string AttributeSource = """
        using System;
        namespace SampleGeneratorGenerated;

        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
        internal sealed class WrapLoggerAttribute : Attribute
        {
        }
        """;
}
