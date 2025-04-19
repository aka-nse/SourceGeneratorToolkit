using Microsoft.CodeAnalysis;

namespace CompilerToolkit.Generators;


partial class SourceWriter
{
    public readonly ref struct Builder()
    {
        private record TypeRegistration(
            TypeIdentifier Type,
            INamedTypeSymbol? Symbol,
            bool IsGenerationTarget);

        private readonly List<TypeRegistration> _types = [];

        /// <summary>
        /// Declares the partial type to generate for <see cref="SourceWriter"/>.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public TypeIdentifier AddTypeGeneration(INamedTypeSymbol type)
        {
            var key = new TypeIdentifier(type);
            _types.Add(new TypeRegistration(key, type, true));
            return key;
        }

        /// <summary>
        /// Declares the file-only type to generate for <see cref="SourceWriter"/>.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="typeKind"></param>
        /// <param name="isRecord"></param>
        /// <returns></returns>
        public TypeIdentifier AddTypeGeneration(string name, TypeKind typeKind = TypeKind.Class, bool isRecord = false)
        {
            var key = new TypeIdentifier(null, null, name, typeKind, isRecord, true);
            _types.Add(new TypeRegistration(key, null, true));
            return key;
        }

        /// <summary>
        /// Declares the type to use in the generated code.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public TypeIdentifier AddTypeUsing(INamedTypeSymbol type)
        {
            var key = new TypeIdentifier(type);
            _types.Add(new TypeRegistration(key, type, false));
            return key;
        }

        public SourceWriter Build()
        {
            INamespaceSymbol[] allNamespaces = [
                .. _types
                    .Select(reg => reg.Symbol?.ContainingNamespace)
                    .Distinct(SymbolEqualityComparer.Default)
                    .OfType<INamespaceSymbol>()
                    .Where(ns => !ns.IsGlobalNamespace),
            ];
            var typeNames = new Dictionary<TypeIdentifier, string>(_types.Count);
            foreach (var type in _types)
            {
                var name = isUnique(allNamespaces, type.Type.Name)
                    ? type.Type.Name
                    : type.Type.FullName;
                var genericMark = name.IndexOf('`');
                if (genericMark != -1)
                {
                    name = name.Substring(0, genericMark);
                }
                typeNames.Add(type.Type, name);
            }
            var typeBuilders = _types
                .Where(t => t.IsGenerationTarget)
                .ToDictionary(t => t.Type, t => new TypeBuilder(t.Type));
            return new(
                [.. allNamespaces
                    .Select(ns => ns.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                ],
                typeBuilders,
                typeNames);

            static bool isUnique(INamespaceSymbol[] namespaces, string shortName)
            {
                var count = 0;
                foreach(var ns in namespaces)
                {
                    count += ns.GetMembers(shortName).Count();
                    if (count > 1)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
