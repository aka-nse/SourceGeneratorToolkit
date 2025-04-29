namespace SourceGeneratorToolkit;

internal static class SourceBuilderExtensions
{
    public static void Append(this ISourceBuilder builder, IEnumerable<CodePart> codeParts)
    {
        foreach (var part in codeParts)
        {
            builder.Append(part);
        }
    }

    public static void Append(this ISourceBuilder builder, SourceStringHandler sourceText)
    {
        foreach (var part in sourceText.CodeParts)
        {
            builder.Append(part);
        }
    }

    public static void Append(this ISourceBuilder sourceBuilder, string text)
    {
        sourceBuilder.Append(new LiteralCodePart(text.AsMemory()));
    }
}
