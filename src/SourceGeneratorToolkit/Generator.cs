using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SourceGeneratorToolkit;

[Generator(LanguageNames.CSharp)]
internal class Generator : IIncrementalGenerator
{
    public static Encoding UTF8N { get; }
        = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static context =>
        {
            var assm = Assembly.GetExecutingAssembly();
            var resourceNames = assm.GetManifestResourceNames();
            foreach (var resourceName in resourceNames)
            {
                if (resourceName.StartsWith("SourceGeneratorToolkit.resoruces.FrameworkCompatibilities."))
                {
                    continue;
                }
                using var stream = assm.GetManifestResourceStream(resourceName);
                var sr = new StreamReader(stream, UTF8N);
                var source = sr.ReadToEnd();
                context.AddSource(resourceName, source);
            }
        });

        new CompatibilityType(
            "System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute",
            "SourceGeneratorToolkit.resoruces.FrameworkCompatibilities.InterpolatedStringHandlerAttribute.cs")
            .Register(context);
        new CompatibilityType(
            "System.Runtime.CompilerServices.IsExternalInit",
            "SourceGeneratorToolkit.resoruces.FrameworkCompatibilities.IsExternalInit.cs")
            .Register(context);
    }
}


file record class CompatibilityType(string FullyQualifiedName, string ResourceName)
{
    public void Register(IncrementalGeneratorInitializationContext context)
    {
        var provider = context
            .CompilationProvider
            .Select(HasType);
        context.RegisterSourceOutput(provider, Emit);
    }

    public bool HasType(Compilation compilation, CancellationToken token)
        => compilation
            .GetTypesByMetadataName(FullyQualifiedName)
            .Any(type => type.DeclaredAccessibility == Accessibility.Public || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly));

    public void Emit(SourceProductionContext context, bool hasType)
    {
        if (hasType)
        {
            return;
        }
        var assm = Assembly.GetExecutingAssembly();
        using var stream = assm.GetManifestResourceStream(ResourceName);
        using var sr = new StreamReader(stream, Generator.UTF8N);
        var source = sr.ReadToEnd();
        context.AddSource(ResourceName, source);
    }
}