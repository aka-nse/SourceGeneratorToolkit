namespace SourceGeneratorToolkit;

internal class CodeScope : IDisposable
{
    private readonly ISourceBuilderState _state;
    private readonly IEnumerable<CodePart> _trailingCode;
    private readonly IDisposable? _containing;

    public CodeScope(
        ISourceBuilderState state,
        IEnumerable<CodePart> leadingCode,
        IEnumerable<CodePart> trailingCode,
        IDisposable? containing = null)
    {
        _state = state;
        _trailingCode = trailingCode;
        _containing = containing;
        if (_state.GetSuspendedCode().Length > 0)
        {
            _state.AppendLine();
        }
        foreach (var part in leadingCode)
        {
            part.AppendTo(_state);
        }
        _state.AppendLine();
        _state.PushIndent("    ");
    }

    public CodeScope(
        ISourceBuilderState state,
        SourceStringHandler leadingCode,
        SourceStringHandler trailingCode,
        IDisposable? containing = null)
        : this(state, leadingCode.CodeParts, trailingCode.CodeParts, containing)
    {
    }

    public void Dispose()
    {
        if (_containing is { })
        {
            _containing.Dispose();
        }
        if (_state.GetSuspendedCode().Length > 0)
        {
            _state.AppendLine();
        }
        _state.PopIndent();
        foreach (var part in _trailingCode)
        {
            part.AppendTo(_state);
        }
        _state.AppendLine();
    }
}
