using System.Collections.Generic;

namespace SourceGeneratorToolkit;

/// <summary>
/// Defines an interface for building source code with indentation and formatting support.
/// </summary>
internal interface ISourceBuilder
{
    /// <summary>
    /// Appends a smart code part object to the source builder.
    /// </summary>
    /// <param name="codePart"></param>
    public void Append(CodePart codePart);

    /// <summary>
    /// Appends a smart code part objects to the source builder.
    /// </summary>
    /// <param name="codeParts"></param>
    public void Append(IEnumerable<CodePart> codeParts);
}
