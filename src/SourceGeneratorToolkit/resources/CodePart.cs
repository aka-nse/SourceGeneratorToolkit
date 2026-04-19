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
    private sealed class NoneCodePart : CodePart
    {
        public override void AppendTo(ISourceBuilderState state)
        {
            // Do nothing
        }
    }

    public static CodePart None { get; } = new NoneCodePart();


    private sealed class LineBreakCodePart : CodePart
    {
        public override void AppendTo(ISourceBuilderState state)
            => state.AppendLine();
    }

    /// <summary>
    /// Gets a line break code part.
    /// </summary>
    public static CodePart LineBreak { get; } = new LineBreakCodePart();



    private sealed class WhereCodePart(Func<ISourceBuilderState, bool> condition, CodePart codePart) : CodePart
    {
        public override void AppendTo(ISourceBuilderState state)
        {
            if(condition(state))
            {
                codePart.AppendTo(state);
            }
        }
    }

    /// <summary>
    /// Gets a code part that appends the given code part only if the given condition is true.
    /// </summary>
    public static CodePart Where(Func<ISourceBuilderState, bool> condition, CodePart codePart) =>
        new WhereCodePart(condition, codePart);


    private sealed class FlushCodePart : CodePart
    {
        public override void AppendTo(ISourceBuilderState state)
        {
            if (state.GetSuspendedCode().Length > 0)
            {
                state.AppendLine();
            }
        }
    }

    public static CodePart Flush { get; } = new FlushCodePart();


    private sealed class PushIndentCodePart(string indent) : CodePart
    {
        public override void AppendTo(ISourceBuilderState state) =>
            state.PushIndent(indent);
    }

    public static CodePart PushIndent(string indent = "    ") => new PushIndentCodePart(indent);


    private sealed class PopIndentCodePart : CodePart
    {
        public override void AppendTo(ISourceBuilderState state) =>
            state.PopIndent();
    }

    public static CodePart PopIndent() => new PopIndentCodePart();


    public static CodePart Literal(ReadOnlyMemory<char> literal) =>
        new LiteralCodePart(literal);

    public static CodePart Literal(string literal) =>
        Literal(literal.AsMemory());

    public abstract void AppendTo(ISourceBuilderState state);

    protected static void AppendMultilinesTo(ISourceBuilderState state, ReadOnlySpan<char> span)
    {
        while (span.Length > 0)
        {
            var lf = span.IndexOf('\n');
            if (lf < 0)
            {
                state.Append(span);
                break;
            }
            if (lf == 0 || (lf == 1 && span[0] == '\r'))
            {
            }
            else if (span[lf - 1] == '\r')
            {
                state.Append(span.Slice(0, lf - 1));
            }
            else
            {
                state.Append(span.Slice(0, lf));
            }
            state.AppendLine();
            span = span.Slice(lf + 1);
        }
    }

    protected static void AppendWithAlignmentTo(ISourceBuilderState state, ReadOnlySpan<char> span, int? alignment)
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
            state.Append(span);
        }
        else
        {
            if (leftAlign)
            {
                state.Append(span);
                state.Append(' ', paddingRequired);
            }
            else
            {
                state.Append(' ', paddingRequired);
                state.Append(span);
            }
        }
    }
}


internal sealed class LiteralCodePart(ReadOnlyMemory<char> literal) : CodePart
{
    public override void AppendTo(ISourceBuilderState state)
        => AppendMultilinesTo(state, literal.Span);

    public override string ToString()
        => $"Literal(\"{literal.ToString()}\")";
}


internal sealed class TypeSymbolCodePart(INamedTypeSymbol type, int? alignment = null) : CodePart
{
    public override void AppendTo(ISourceBuilderState state)
    {
        var name = state.GetDisplayName(type);
        AppendWithAlignmentTo(state, name.AsSpan(), alignment);
    }

    public override string ToString()
        => $"TypeSymbol(\"{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}\")";
}


internal sealed class FormattedCodePart<T>(T value, int? alignment = null, string? format = null) : CodePart
{
    public override void AppendTo(ISourceBuilderState state)
    {
        string formattedValue;
        if (value is IFormattable formattable)
        {
            formattedValue = formattable.ToString(format, state.FormatProvider);
        }
        else
        {
            formattedValue = value?.ToString() ?? string.Empty;
        }
        AppendWithAlignmentTo(state, formattedValue.AsSpan(), alignment);
    }

    public override string ToString()
        => $"Formatted(\"{value}\")";
}


internal sealed class LazyCodePart(CodePart? firstTimePrefix, CodePart? separator, CodePart? lastTimeSuffix) : CodePart
{
    private readonly List<CodePart> _parts = [];

    public void Add(CodePart part)
        => _parts.Add(part);

    public void Add(IEnumerable<CodePart> parts)
        => _parts.AddRange(parts);

    public void Add(SourceStringHandler code)
        => _parts.AddRange(code.CodeParts);

    public override void AppendTo(ISourceBuilderState state)
    {
        if(_parts.Count == 0)
        {
            return;
        }

        separator ??= CodePart.None;
        var currentSeparator = None;
        firstTimePrefix?.AppendTo(state);
        foreach (var part in _parts)
        {
            currentSeparator.AppendTo(state);
            currentSeparator = separator;
            part.AppendTo(state);
        }
        lastTimeSuffix?.AppendTo(state);
    }
}





internal sealed class CaptureIndentedCodePart(IEnumerable<CodePart> codeParts) : CodePart
{
    public override void AppendTo(ISourceBuilderState state)
    {
        var currentLineLeading = state.GetSuspendedCode();
        var indent = currentLineLeading.ToString();
        currentLineLeading.Clear();
        state.PushIndent(indent);
        foreach (var codePart in codeParts)
        {
            codePart.AppendTo(state);
        }
        state.PopIndent();
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
