using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace SourceGeneratorToolkit;


/// <summary>
/// Represents a part of code that can be appended to a source builder.
/// </summary>
internal abstract class CodePart
{
    private sealed class LineBreakCodePart : CodePart
    {
        public override void AppendTo(ISourceBuilderState sourceBuilder)
            => sourceBuilder.AppendLine();
    }

    /// <summary>
    /// Gets a line break code part.
    /// </summary>
    public static CodePart LineBreak { get; } = new LineBreakCodePart();

    public abstract void AppendTo(ISourceBuilderState sourceBuilder);

    protected static void AppendMultilinesTo(ISourceBuilderState sourceBuilder, ReadOnlySpan<char> span)
    {
        while (span.Length > 0)
        {
            var lf = span.IndexOf('\n');
            if (lf < 0)
            {
                sourceBuilder.Append(span);
                break;
            }
            if (lf == 0 || (lf == 1 && span[0] == '\r'))
            {
            }
            else if (span[lf - 1] == '\r')
            {
                sourceBuilder.Append(span.Slice(0, lf - 1));
            }
            else
            {
                sourceBuilder.Append(span.Slice(0, lf));
            }
            sourceBuilder.AppendLine();
            span = span.Slice(lf + 1);
        }
    }

    protected static void AppendWithAlignmentTo(ISourceBuilderState sourceBuilder, ReadOnlySpan<char> span, int? alignment)
    {
        var align = alignment ?? 0;
        var leftAlign = false;
        if (align < 0)
        {
            leftAlign = true;
            align = -align;
        }

        var paddingRequired = align - span.Length;
        if (paddingRequired <= 0)
        {
            sourceBuilder.Append(span);
        }
        else
        {
            if (leftAlign)
            {
                sourceBuilder.Append(span);
                sourceBuilder.Append(' ', paddingRequired);
            }
            else
            {
                sourceBuilder.Append(' ', paddingRequired);
                sourceBuilder.Append(span);
            }
        }
    }
}


internal sealed class LiteralCodePart(ReadOnlyMemory<char> literal) : CodePart
{
    public override void AppendTo(ISourceBuilderState sourceBuilder)
        => AppendMultilinesTo(sourceBuilder, literal.Span);

    public override string ToString()
        => $"Literal(\"{literal.ToString()}\")";
}


internal sealed class TypeSymbolCodePart(INamedTypeSymbol type, int? alignment = null) : CodePart
{
    public override void AppendTo(ISourceBuilderState sourceBuilder)
    {
        var name = sourceBuilder.GetDisplayName(type);
        AppendWithAlignmentTo(sourceBuilder, name.AsSpan(), alignment);
    }

    public override string ToString()
        => $"TypeSymbol(\"{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}\")";
}


internal sealed class FormattedCodePart<T>(T value, int? alignment = null, string? format = null) : CodePart
{
    public override void AppendTo(ISourceBuilderState sourceBuilder)
    {
        string formattedValue;
        if (value is IFormattable formattable)
        {
            formattedValue = formattable.ToString(format, sourceBuilder.FormatProvider);
        }
        else
        {
            formattedValue = value?.ToString() ?? string.Empty;
        }
        AppendWithAlignmentTo(sourceBuilder, formattedValue.AsSpan(), alignment);
    }

    public override string ToString()
        => $"Formatted(\"{value}\")";
}


internal sealed class IndentedCodePart(string indent, IEnumerable<CodePart> codeParts) : CodePart
{
    public override void AppendTo(ISourceBuilderState sourceBuilder)
    {
        sourceBuilder.PushIndent(indent);
        foreach (var codePart in codeParts)
        {
            codePart.AppendTo(sourceBuilder);
        }
        sourceBuilder.PopIndent();
    }

    public override string ToString()
        => $"Indent(constant \"{indent}\")";
}


internal sealed class CaptureIndentedCodePart(IEnumerable<CodePart> codeParts) : CodePart
{
    public override void AppendTo(ISourceBuilderState sourceBuilder)
    {
        var currentLineLeading = sourceBuilder.GetSuspendedCode();
        var indent = currentLineLeading.ToString();
        currentLineLeading.Clear();
        sourceBuilder.PushIndent(indent);
        foreach (var codePart in codeParts)
        {
            codePart.AppendTo(sourceBuilder);
        }
        sourceBuilder.PopIndent();
    }

    public override string ToString()
        => $"Indent(capture)";
}


internal static class CodePartExtensions
{
    private static CodePart PreserveIndentCore(this IEnumerable<IEnumerable<CodePart>> codeBlocks)
    {
        var list = new List<CodePart>();
        var iter = codeBlocks.GetEnumerator();
        if(iter.MoveNext())
        {
            list.AddRange(iter.Current);
            while(iter.MoveNext())
            {
                list.Add(CodePart.LineBreak);
                list.AddRange(iter.Current);
            }
        }
        return new CaptureIndentedCodePart(list);
    }

    /// <summary>
    /// Captures the current indent and appends the sequence with the indent.
    /// </summary>
    /// <param name="codeBlocks"></param>
    /// <returns></returns>
    public static CodePart PreserveIndent(this SourceStringHandler codeBlocks)
        => new CaptureIndentedCodePart(codeBlocks.CodeParts);

    /// <summary>
    /// Captures the current indent and appends the sequence with the indent.
    /// </summary>
    /// <param name="codeBlocks"></param>
    /// <returns></returns>
    public static CodePart PreserveIndent(this IEnumerable<IEnumerable<CodePart>> codeBlocks)
        => PreserveIndentCore(codeBlocks);

    /// <summary>
    /// Captures the current indent and appends the sequence with the indent.
    /// </summary>
    /// <param name="codeBlocks"></param>
    /// <returns></returns>
    public static CodePart PreserveIndent(this IEnumerable<SourceStringHandler> codeBlocks)
        => PreserveIndentCore(codeBlocks.Select(cb => cb.CodeParts));

    /// <summary>
    /// Captures the current indent and appends the sequence with the indent.
    /// </summary>
    /// <param name="codeBlocks"></param>
    /// <returns></returns>
    public static CodePart PreserveIndent(this IEnumerable<string> codeBlocks)
        => PreserveIndentCore(codeBlocks.Select(cb => ((SourceStringHandler)$"{cb}").CodeParts));

    /// <summary>
    /// Captures the current indent and appends the sequence with the indent.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="codeBlocks"></param>
    /// <returns></returns>
    public static CodePart PreserveIndent<T>(this IEnumerable<T> codeBlocks)
        => PreserveIndentCore(codeBlocks.Select(cb => ((SourceStringHandler)$"{cb}").CodeParts));
}
