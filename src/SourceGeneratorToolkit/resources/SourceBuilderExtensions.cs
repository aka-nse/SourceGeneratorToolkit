using System;
using System.Collections.Generic;
namespace SourceGeneratorToolkit;

internal static class SourceBuilderExtensions
{
    /// <summary>
    /// Appends a sequence of <see cref="CodePart"/> to the <see cref="ISourceBuilder"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="codeParts"></param>
    public static void Append(this ISourceBuilder builder, IEnumerable<CodePart> codeParts)
    {
        foreach (var part in codeParts)
        {
            builder.Append(part);
        }
    }

    /// <summary>
    /// Appends a formatted string to the <see cref="ISourceBuilder"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="sourceText"></param>
    public static void Append(this ISourceBuilder builder, SourceStringHandler sourceText)
    {
        foreach (var part in sourceText.CodeParts)
        {
            builder.Append(part);
        }
    }

    /// <summary>
    /// Appends a string to the <see cref="ISourceBuilder"/> as a literal.
    /// </summary>
    /// <param name="sourceBuilder"></param>
    /// <param name="text"></param>
    public static void Append(this ISourceBuilder sourceBuilder, string text)
    {
        sourceBuilder.Append(new LiteralCodePart(text.AsMemory()));
    }

    /// <summary>
    /// Appends a formatted string with line break terminator to the <see cref="ISourceBuilder"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="sourceText"></param>
    public static void AppendLine(this ISourceBuilder builder, SourceStringHandler sourceText)
    {
        builder.Append(sourceText);
        builder.AppendLine();
    }

    /// <summary>
    /// Appends a string with line break terminator to the <see cref="ISourceBuilder"/> as a literal.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="text"></param>
    public static void AppendLine(this ISourceBuilder builder, string text)
    {
        builder.Append(text);
        builder.AppendLine();
    }
}
