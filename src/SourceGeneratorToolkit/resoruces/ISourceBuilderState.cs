using System.Text;
using Microsoft.CodeAnalysis;

namespace SourceGeneratorToolkit;

internal interface ISourceBuilderState
{
    /// <summary>
    /// Gets format provider for formatting values.
    /// </summary>
    public IFormatProvider FormatProvider { get; }

    /// <summary>
    /// Formats the type symbol into a string which can be used in source code.
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public string GetDisplayName(INamedTypeSymbol symbol);

    /// <summary>
    /// Appends a value to the source builder.
    /// Do not contains line break.
    /// </summary>
    public void Append(ReadOnlySpan<char> value);

    /// <inheritdoc cref="Append(ReadOnlySpan{T})"/>
    public void Append(ReadOnlyMemory<char> value);

    /// <inheritdoc cref="Append(ReadOnlySpan{T})"/>
    public void Append(string value);

    /// <inheritdoc cref="Append(ReadOnlySpan{T})"/>
    public void Append(char c, int repeatCount);

    /// <summary>
    /// Finalizes the contents of current line and append line terminator to the tail of them.
    /// </summary>
    public void AppendLine();

    /// <summary>
    /// Introduces a new indentation to the lines after at here.
    /// </summary>
    /// <param name="indent"></param>
    public void PushIndent(string indent);

    /// <summary>
    /// Removes the last indentation added by <see cref="PushIndent(string)"/>.
    /// </summary>
    public void PopIndent();

    /// <summary>
    /// Retrieves a collection of code parts that are pending output until the next call to <see cref="AppendLine"/>.
    /// </summary>
    /// <returns>A sequence of instances representing the suspended elements.</returns>
    public StringBuilder GetSuspendedCode();
}
