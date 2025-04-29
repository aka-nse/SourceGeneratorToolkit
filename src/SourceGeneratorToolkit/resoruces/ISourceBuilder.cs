namespace SourceGeneratorToolkit;

internal interface ISourceBuilder
{
    public void Append(CodePart codePart);
    public void AppendLine();
    public void PushIndent(string indent);
    public void PopIndent();
}
