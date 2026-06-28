using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace SourceGeneratorToolkit;

internal class SourceBuilderSlim : ISourceBuilder
{
    private class State(SourceBuilderSlim owner) : ISourceBuilderState
    {
        private readonly StringBuilder _sourceCode = new();
        public readonly StringBuilder _suspendedCode = new();
        private readonly Stack<string> _indentStack = [];
        private string _currentIndent = "";

        public IFormatProvider FormatProvider => owner.FormatProvider;

        public int IndentLevel => _indentStack.Count;

        private void PushSuspendedCode()
        {
            if (_suspendedCode.Length > 0)
            {
                _sourceCode.Append(_currentIndent);
                _sourceCode.Append(_suspendedCode);
                _suspendedCode.Clear();
            }
        }

        public void Append(ReadOnlySpan<char> value)
        {
            _suspendedCode.Append(value);
        }

        public void Append(ReadOnlyMemory<char> value)
        {
            _suspendedCode.Append(value.Span);
        }

        public void Append(string value)
        {
            _suspendedCode.Append(value);
        }

        public void Append(char c, int repeatCount)
        {
            _suspendedCode.Append(c, repeatCount);
        }

        public void AppendLine()
        {
            PushSuspendedCode();
            _sourceCode.AppendLine();
        }

        public string GetDisplayName(INamedTypeSymbol symbol)
            => owner.GetDisplayName(symbol);

        public StringBuilder GetSuspendedCode()
            => _suspendedCode;

        public void PushIndent(string indent)
        {
            _indentStack.Push(indent);
            _currentIndent = string.Concat(_indentStack);
        }

        public StringBuilder GetStringBuilder()
        {
            PushSuspendedCode();
            return _sourceCode;
        }

        public override string ToString()
        {
            PushSuspendedCode();
            return _sourceCode.ToString();
        }
    }


    private readonly List<CodePart> _codeParts = [];

    public IFormatProvider FormatProvider { get; }

    public SourceBuilderSlim(IFormatProvider? formatProvider = null)
    {
        FormatProvider = formatProvider ?? CultureInfo.InvariantCulture;
    }

    public string GetDisplayName(INamedTypeSymbol symbol)
        => symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    public void Append(CodePart codePart)
        => _codeParts.Add(codePart);

    public void Append(IEnumerable<CodePart> codeParts)
        => _codeParts.AddRange(codeParts);

    public StringBuilder BuildStringBuilder()
    {
        var state = new State(this);
        foreach (var codePart in _codeParts)
        {
            codePart.AppendTo(state);
        }
        return state.GetStringBuilder();
    }

    public string Build() =>
        BuildStringBuilder().ToString();
}