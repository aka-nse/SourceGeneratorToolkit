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
    /// Appends a line break;
    /// </summary>
    public void AppendLine();

    /// <summary>
    /// Begins a new indented region.
    /// </summary>
    /// <param name="indent"></param>
    public void PushIndent(string indent);

    /// <summary>
    /// Finishes indented region which last entered.
    /// </summary>
    public void PopIndent();
}
