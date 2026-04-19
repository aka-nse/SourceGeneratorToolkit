using System;
using System.Collections.Generic;

namespace SourceGeneratorToolkit;

internal class TypeDeclarationScope : IDisposable
{
    private readonly ISourceBuilder _builder;
    private readonly LazyCodePart _baseTypesCodePart;
    private readonly LazyCodePart _attributesCodePart;
    private readonly IEnumerable<CodePart> _leadingCode;
    private readonly IEnumerable<CodePart> _trailingCode;
    private readonly IDisposable? _containing;

    public TypeDeclarationScope(
        ISourceBuilder builder,
        Func<(LazyCodePart baseTypes, LazyCodePart attributes), SourceStringHandler> leadingCode,
        SourceStringHandler trailingCode,
        IDisposable? containing = null)
    {
        _builder = builder;
        _baseTypesCodePart = new LazyCodePart(CodePart.Literal(" : "), CodePart.Literal(", "), null);
        _attributesCodePart = new LazyCodePart(null, CodePart.LineBreak, null);
        _leadingCode = leadingCode((_baseTypesCodePart, _attributesCodePart)).CodeParts;
        _trailingCode = trailingCode.CodeParts;
        _containing = containing;
        _builder.Append(CodePart.Flush);
        _builder.Append(_leadingCode);
        _builder.AppendLine();
        _builder.PushIndent("    ");
    }


    public void Dispose()
    {
        if (_containing is { })
        {
            _containing.Dispose();
        }
        _builder.Append(CodePart.Flush);
        _builder.Append(CodePart.PopIndent());
        _builder.Append(_trailingCode);
        _builder.Append(CodePart.LineBreak);
        _builder.Append(CodePart.Where(static state => state.IndentLevel == 0, CodePart.LineBreak));
    }

    public void AddBaseType(string baseType)
    {
        _baseTypesCodePart.Add(CodePart.Literal(baseType));
    }

    public void AddBaseType(SourceStringHandler baseType)
    {
        _baseTypesCodePart.Add(baseType);
    }

    public void AddAttribute(string attribute)
    {
        _attributesCodePart.Add(CodePart.Literal($"[{attribute}]"));
    }

    public void AddAttribute(SourceStringHandler attribute)
    {
        _attributesCodePart.Add([
            CodePart.Literal("["),
            .. attribute.CodeParts,
            CodePart.Literal("]"),
        ]);
    }
}
