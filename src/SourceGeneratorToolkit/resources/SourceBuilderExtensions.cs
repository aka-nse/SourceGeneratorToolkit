using System;
using System.Collections.Generic;
namespace SourceGeneratorToolkit;

internal static class SourceBuilderExtensions
{
    /// <summary>
    /// Appends a line break;
    /// </summary>
    public static void AppendLine(this ISourceBuilder sourceBuilder)
        => sourceBuilder.Append(CodePart.LineBreak);

    /// <summary>
    /// Begins a new indented region.
    /// </summary>
    /// <param name="indent"></param>
    public static void PushIndent(this ISourceBuilder sourceBuilder, string indent)
        => sourceBuilder.Append(CodePart.PushIndent(indent));

    /// <summary>
    /// Finishes indented region which last entered.
    /// </summary>
    public static void PopIndent(this ISourceBuilder sourceBuilder)
        => sourceBuilder.Append(CodePart.PopIndent());

    /// <summary>
    /// Flushes the suspended codes with terminal line break.
    /// </summary>
    /// <param name="sourceBuilder"></param>
    public static void Flush(this ISourceBuilder sourceBuilder)
        => sourceBuilder.Append(CodePart.Flush);

    /// <summary>
    /// Appends a formatted string to the <see cref="ISourceBuilder"/>.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="sourceText"></param>
    public static void Append(this ISourceBuilder builder, SourceStringHandler sourceText) =>
        builder.Append(sourceText.CodeParts);

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
