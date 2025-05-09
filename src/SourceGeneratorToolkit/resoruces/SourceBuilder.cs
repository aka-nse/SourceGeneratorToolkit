using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SourceGeneratorToolkit;

/// <summary>
/// Provides a source code builder for source generation.<br/>
/// <c>Append()</c> with interpolated string literal will be optimized to code generation.
/// Please see <see cref="SourceStringHandler"/>.
/// </summary>
/// <example><![CDATA[
/// public static void Emit(SourceProductionContext context, GeneratorAttributeSyntaxContext source)
/// {
///     var builder = new SourceBuilder(source);
///     using (builder.BeginTargetTypeDeclare())
///     {
///         builder.AppendLine($$"""
///             public static string SayHello()
///                 => "Hello, " + typeof({{(INamedTypeSymbol)source.TargetSymbol}});
///             """);
///     }
///     context.AddSource(
///         builder.GetPreferHintName(prefix: "", suffix: ""),
///         builder.Build()
///         );
/// }
/// ]]></example>
internal partial class SourceBuilder : ISourceBuilder
{
    private class State(SourceBuilder owner) : ISourceBuilderState
    {
        private readonly StringBuilder _sourceCode = new();
        public readonly StringBuilder _suspendedCode = new();
        private readonly Stack<string> _indentStack = [];
        private string _currentIndent = "";

        public IFormatProvider FormatProvider => owner.FormatProvider;

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

        public void PopIndent()
        {
            PushSuspendedCode();
            _indentStack.Pop();
            _currentIndent = string.Concat(_indentStack);
        }

        public override string ToString()
            => _sourceCode.ToString() + _suspendedCode.ToString();
    }


    private readonly State _state;
    private readonly SemanticModel _semanticModel;
    private readonly int _nameSearchPosition;
    private readonly SyntaxList<UsingDirectiveSyntax> _usings;

    public GeneratorAttributeSyntaxContext Context { get; }

    public IFormatProvider FormatProvider { get; }

    public INamespaceSymbol TargetNamespace => TargetType.ContainingNamespace;

    public INamedTypeSymbol TargetType { get; }

    public CodePart AutoGeneratedComment
        => _autoGeneratedCommend;
    private static readonly CodePart _autoGeneratedCommend = new LiteralCodePart("// <auto-generated/>".AsMemory());

    public CodePart DefaultUsingDirectives
    {
        get
        {
            if(_defaultUsingDirectives is null)
            {
                var usings = string.Join("\n", _usings.Select(static x => x.ToString()));
                _defaultUsingDirectives = new LiteralCodePart(usings.AsMemory());
            }
            return _defaultUsingDirectives ??= new LiteralCodePart(_usings.ToString().AsMemory());
        }
    }

    private CodePart? _defaultUsingDirectives;

    public CodePart NamespaceDeclaration
    {
        get
        {
            return _namespaceDeclaration ??= new LiteralCodePart(core().AsMemory());

            string core()
                => TargetType.ContainingNamespace is { } ns && !ns.IsGlobalNamespace
                ? $"namespace {TargetType.ContainingNamespace.ToDisplayString()};\n"
                : "";
        }
    }
    private CodePart? _namespaceDeclaration;

    public SourceBuilder(GeneratorAttributeSyntaxContext context, bool usesDefaultHeader = true)
        : this(context, CultureInfo.InvariantCulture, usesDefaultHeader)
    {
    }

    public SourceBuilder(
        GeneratorAttributeSyntaxContext context,
        IFormatProvider formatProvider,
        bool usesDefaultHeader = true)
    {
        Context = context;
        FormatProvider = formatProvider;
        TargetType = (context.TargetSymbol as INamedTypeSymbol)
            ?? context.TargetSymbol.ContainingType;
        if (TargetType is null)
        {
            throw new ArgumentException("Target type is null.");
        }
        _semanticModel = context.SemanticModel;

        var cu = context.TargetNode
            .Ancestors()
            .OfType<CompilationUnitSyntax>()
            .First();
        _usings = cu.Usings;
        _nameSearchPosition = cu.ChildNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault()
            ?.ChildNodes()
            .FirstOrDefault()
            ?.SpanStart
            ?? _usings.FullSpan.End;
        _state = new State(this);
        if (usesDefaultHeader)
        {
            AppendAutoGeneratedComment();
            AppendDefaultUsingDirectives();
            AppendNamespaceDeclaration();
            AppendLine();
        }
    }


    public string GetPreferHintName(string? prefix = null, string? suffix = null)
    {
        static void writeType(StringBuilder sb, ITypeSymbol type)
        {
            if (type is INamedTypeSymbol namedType)
            {
                sb.Append(namedType.Name);
                if (namedType.TypeArguments.Length > 0)
                {
                    sb.Append($"`{namedType.TypeArguments.Length}");
                }
            }
            else
            {
                sb.Append(type.Name);
            }
        }

        var symbolId = new StringBuilder();
        if (!TargetNamespace.IsGlobalNamespace)
        {
            symbolId.Append($"{TargetNamespace.Name}.");
        }
        foreach (var ancestor in GetAncestors(TargetType.ContainingType))
        {
            writeType(symbolId, ancestor);
            symbolId.Append('+');
        }
        writeType(symbolId, TargetType);
        if (Context.TargetSymbol is IMethodSymbol methodSymbol)
        {
            symbolId.Append($".{methodSymbol.Name}(");
            var separator = "";
            foreach (var param in methodSymbol.Parameters)
            {
                symbolId.Append(separator);
                separator = "-";
                writeType(symbolId, param.Type);
            }
            symbolId.Append(')');
        }
        return $"{prefix}{symbolId}{suffix}.cs";
    }


    public string GetDisplayName(INamedTypeSymbol symbol)
        => symbol.ToMinimalDisplayString(_semanticModel, _nameSearchPosition, SymbolDisplayFormat.MinimallyQualifiedFormat);

    public void PushIndent(string indent)
    {
        _state.PushIndent(indent);
    }

    public void PopIndent()
    {
        _state.PopIndent();
    }

    public void Append(CodePart codePart)
    {
        codePart.AppendTo(_state);
    }

    public void AppendLine()
    {
        _state.AppendLine();
    }

    public void AppendAutoGeneratedComment()
    {
        AutoGeneratedComment.AppendTo(_state);
        _state.AppendLine();
    }

    public void AppendDefaultUsingDirectives()
    {
        DefaultUsingDirectives.AppendTo(_state);
        _state.AppendLine();
    }

    public void AppendNamespaceDeclaration()
    {
        NamespaceDeclaration.AppendTo(_state);
        _state.AppendLine();
    }

    public IDisposable BeginTargetTypeDeclare()
        => GetPartialTypeDecl(TargetType);

    public IDisposable BeginFileOnlyTypeDeclare(
        string typeName,
        TypeKind typeKind = TypeKind.Class,
        bool isRecord = false,
        params string[] typeArguments)
    {
        var keyword = GetKeyword(typeKind, isRecord);
        var typeArgumentsDecl = typeArguments.Length > 0
            ? ("<" + string.Join(", ", typeArguments) + ">")
            : "";
        return new CodeScope(
            _state,
            $$"""
            {{keyword}} {{typeName}}{{typeArgumentsDecl}}
            {
            """,
            $$"""
            }
            """);
    }

    public string Build()
        => _state.ToString();


    private CodeScope GetPartialTypeDecl(INamedTypeSymbol type)
    {
        var keyword = GetKeyword(type.TypeKind, type.IsRecord);
        var typeArguments = type.TypeArguments.Length > 0
            ? ("<" + string.Join(", ", type.TypeArguments.Select(ta => ta.Name)) + ">")
            : "";
        var containing = type.ContainingType is { }
            ? GetPartialTypeDecl(type.ContainingType)
            : null;
        return new CodeScope(
            _state,
            $$"""
            partial {{keyword}} {{type.Name}}{{typeArguments}}
            {
            """,
            $$"""
            }
            """,
            containing);
    }

    private static string GetKeyword(TypeKind typeKind, bool isRecord)
        => (typeKind, isRecord) switch
        {
            (TypeKind.Class, false) => "class",
            (TypeKind.Struct, false) => "struct",
            (TypeKind.Interface, false) => "interface",
            (TypeKind.Class, true) => "record class",
            (TypeKind.Struct, true) => "record struct",
            _ => throw new NotSupportedException(),
        };

    private static IEnumerable<INamedTypeSymbol> GetAncestors(INamedTypeSymbol? type)
    {
        if (type is null)
        {
            yield break;
        }
        foreach (var ancestor in GetAncestors(type.ContainingType))
        {
            yield return ancestor;
        }
        yield return type;
    }
}
