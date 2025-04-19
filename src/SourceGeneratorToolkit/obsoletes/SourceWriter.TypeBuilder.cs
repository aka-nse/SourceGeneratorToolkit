using System.Text;
using Microsoft.CodeAnalysis;

namespace CompilerToolkit.Generators;


public interface ITypeBuilder
{
    TypeIdentifier TargetType { get; }
    ITypeBuilder AddGenericTypeArgument(string name, TypeMemberSourceHandler restriction = default);
    ITypeBuilder AddAttribute(TypeIdentifier attributeType, TypeMemberSourceHandler arguments = default);
    ITypeBuilder AddRawMember(TypeMemberSourceHandler source);
}


partial class SourceWriter
{
    private sealed record class TypeBuilder(TypeIdentifier TargetType)
        : ITypeBuilder, IEquatable<TypeBuilder>
    {
        private sealed record class AttributeInfo(
            TypeIdentifier AttributeType,
            IReadOnlyList<TypeMemberSourceHandler.CodePart> Arguments)
        {
            public bool Equals(AttributeInfo other)
            {
                if(other is null)
                {
                    return false;
                }
                if (!AttributeType.Equals(other.AttributeType))
                {
                    return false;
                }
                if (!Arguments.SequenceEqual(other.Arguments))
                {
                    return false;
                }
                return true;
            }

            public override int GetHashCode()
                => AttributeType.GetHashCode() ^ Arguments.Count;
        }

        private sealed record class TypeArgumentInfo(
            string Name,
            IReadOnlyList<TypeMemberSourceHandler.CodePart> CodeParts)
        {
            public bool Equals(TypeArgumentInfo other)
            {
                if (other is null)
                {
                    return false;
                }
                if (Name != other.Name)
                {
                    return false;
                }
                if (!CodeParts.SequenceEqual(other.CodeParts))
                {
                    return false;
                }
                return true;
            }

            public override int GetHashCode()
                => Name.GetHashCode() ^ CodeParts.Count;
        }


        private readonly List<AttributeInfo> _attributes = [];

        private readonly List<TypeArgumentInfo> _typeArguments = [];

        private readonly List<TypeMemberSourceHandler.CodePart> _memberParts = [];

        public bool Equals(TypeBuilder other)
        {
            if(other is null)
            {
                return false;
            }
            if(!TargetType.Equals(other.TargetType))
            {
                return false;
            }
            if (!_attributes.SequenceEqual(other._attributes))
            {
                return false;
            }
            if (!_typeArguments.SequenceEqual(other._typeArguments))
            {
                return false;
            }
            if (!_memberParts.SequenceEqual(other._memberParts))
            {
                return false;
            }
            return true;
        }

        public override int GetHashCode()
            => TargetType.GetHashCode() ^ _attributes.Count ^ _typeArguments.Count ^ _memberParts.Count;


        public void Generate(SourceBuilderState state, StringBuilder sb)
        {
            var ancestors = TypeIdentifier.Ancestors(TargetType);
            sb.AppendJoin(" ", ancestors.Select(ancestor =>
            {
                var keyword = Keyword(ancestor.TypeKind, ancestor.IsRecord);
                return $"partial {keyword} {ancestor.Name} {{";
            }));
            sb.AppendLine();
            foreach (var attr in _attributes)
            {
                sb.Append($"[{state.GetDisplayName(attr.AttributeType)}(");
                foreach(var part in attr.Arguments)
                {
                    part.Generate(state, sb);
                }
                sb.AppendLine(")]");
            }
            sb.Append(TargetType.IsFileOnly ? "file " : "partial ");
            sb.Append(Keyword(TargetType.TypeKind, TargetType.IsRecord));
            sb.AppendLine($" {TargetType.Name}");
            sb.AppendLine("{");

            foreach (var memberPart in _memberParts)
            {
                memberPart.Generate(state, sb);
            }
            sb.AppendLine("}");
            sb.AppendJoin(" ", ancestors.Select(_ => "}"));
            sb.AppendLine();
        }

        ITypeBuilder ITypeBuilder.AddAttribute(TypeIdentifier attributeType, TypeMemberSourceHandler arguments)
        {
            _attributes.Add(new(attributeType, arguments.CodeParts));
            return this;
        }

        ITypeBuilder ITypeBuilder.AddGenericTypeArgument(string name, TypeMemberSourceHandler restriction)
        {
            _typeArguments.Add(new(name, restriction.CodeParts));
            return this;
        }

        ITypeBuilder ITypeBuilder.AddRawMember(TypeMemberSourceHandler source)
        {
            _memberParts.AddRange(source.CodeParts);
            _memberParts.Add(TypeMemberSourceHandler.LineBreak);
            return this;
        }

        private static string Keyword(TypeKind typeKind, bool IsRecord)
            => (IsRecord ? "record " : "")
            + (typeKind switch
            {
                TypeKind.Class => "class",
                TypeKind.Struct => "struct",
                TypeKind.Interface => "interface",
                _ => throw new NotSupportedException(),
            });
    }
}
