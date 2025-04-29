using Microsoft.CodeAnalysis;

namespace SourceGeneratorToolkit;


internal abstract class CodePart
{
    private sealed class LineBreakCodePart : CodePart
    {
        public override void AppendTo(ISourceBuilderState sourceBuilder)
            => sourceBuilder.AppendLine();
    }

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
}


internal sealed class TypeSymbolCodePart(INamedTypeSymbol type, int? alignment = null) : CodePart
{
    public override void AppendTo(ISourceBuilderState sourceBuilder)
    {
        var name = sourceBuilder.GetDisplayName(type);
        AppendWithAlignmentTo(sourceBuilder, name.AsSpan(), alignment);
    }
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
}


internal static class CodePartExtensions
{
    public static CodePart IndentForeach(this IEnumerable<string> codeBlocks)
    {
        var list = new List<CodePart>();
        foreach (var codeBlock in codeBlocks)
        {
            SourceStringHandler handled = $"{codeBlock}{CodePart.LineBreak}";
            list.AddRange(handled.CodeParts);
        }
        return new CaptureIndentedCodePart(list);
    }

    public static CodePart IndentForeach(this IEnumerable<IEnumerable<CodePart>> codeBlocks)
    {
        var list = new List<CodePart>();
        foreach (var codeBlock in codeBlocks)
        {
            list.AddRange(codeBlock);
            list.Add(CodePart.LineBreak);
        }
        return new CaptureIndentedCodePart(list);
    }

    public static CodePart IndentForeach(this IEnumerable<SourceStringHandler> codeBlocks)
    {
        var list = new List<CodePart>();
        foreach (var codeBlock in codeBlocks)
        {
            list.AddRange(codeBlock.CodeParts);
            list.Add(CodePart.LineBreak);
        }
        return new CaptureIndentedCodePart(list);
    }
}
