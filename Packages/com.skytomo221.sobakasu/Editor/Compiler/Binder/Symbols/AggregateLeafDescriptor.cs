using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class AggregateLeafDescriptor
    {
        public TypeSymbol Type { get; }
        public IReadOnlyList<string> Path { get; }
        public string PathText => string.Join(".", Path);
        public bool IsEnumTag { get; }

        public AggregateLeafDescriptor(
            TypeSymbol type,
            IReadOnlyList<string> path,
            bool isEnumTag = false)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            IsEnumTag = isEnumTag;
        }
    }

    internal static class AggregateLayout
    {
        public const string EnumTagPathSegment = "tag";

        public static IReadOnlyList<AggregateLeafDescriptor> GetLeaves(TypeSymbol type)
        {
            var leaves = new List<AggregateLeafDescriptor>();
            AppendLeaves(type, Array.Empty<string>(), leaves, new HashSet<TypeSymbol>());
            return leaves;
        }

        public static IReadOnlyList<int> GetFieldLeafIndices(
            TypeSymbol containingType,
            AggregateFieldSymbol field)
        {
            var result = new List<int>();
            var leaves = GetLeaves(containingType);
            for (var index = 0; index < leaves.Count; index++)
            {
                if (leaves[index].Path.Count > 0 &&
                    string.Equals(leaves[index].Path[0], field.Name, StringComparison.Ordinal))
                {
                    result.Add(index);
                }
            }
            return result;
        }

        public static IReadOnlyList<int> GetEnumVariantLeafIndices(
            TypeSymbol enumType,
            EnumVariantSymbol variant)
        {
            var result = new List<int>();
            var leaves = GetLeaves(enumType);
            for (var index = 0; index < leaves.Count; index++)
            {
                if (!leaves[index].IsEnumTag &&
                    leaves[index].Path.Count > 0 &&
                    string.Equals(leaves[index].Path[0], variant.Name, StringComparison.Ordinal))
                {
                    result.Add(index);
                }
            }
            return result;
        }

        private static void AppendLeaves(
            TypeSymbol type,
            IReadOnlyList<string> path,
            ICollection<AggregateLeafDescriptor> leaves,
            ISet<TypeSymbol> visiting)
        {
            if (type == null || type == TypeSymbol.Error)
                return;

            if (type.TypeKind == TypeKind.Array && type.ElementType?.UsesFlattenedAggregateStorage == true)
            {
                var elementLeaves = new List<AggregateLeafDescriptor>();
                AppendLeaves(
                    type.ElementType,
                    Array.Empty<string>(),
                    elementLeaves,
                    visiting);
                foreach (var elementLeaf in elementLeaves)
                {
                    leaves.Add(new AggregateLeafDescriptor(
                        TypeSymbol.Array(elementLeaf.Type),
                        Combine(path, elementLeaf.Path),
                        elementLeaf.IsEnumTag));
                }
                return;
            }

            if (!type.UsesFlattenedAggregateStorage)
            {
                leaves.Add(new AggregateLeafDescriptor(type, path));
                return;
            }

            if (!visiting.Add(type))
                return;

            if (type.AggregateKind == UserAggregateKind.Struct ||
                type.AggregateKind == UserAggregateKind.Tuple)
            {
                foreach (var field in type.AggregateFields)
                    AppendLeaves(field.Type, Append(path, field.Name), leaves, visiting);
            }
            else
            {
                leaves.Add(new AggregateLeafDescriptor(
                    TypeSymbol.I32,
                    Append(path, EnumTagPathSegment),
                    isEnumTag: true));
                foreach (var variant in type.EnumVariants)
                    foreach (var field in variant.Fields)
                    {
                        AppendLeaves(
                            field.Type,
                            Append(Append(path, variant.Name), field.Name),
                            leaves,
                            visiting);
                    }
            }

            visiting.Remove(type);
        }

        private static IReadOnlyList<string> Append(
            IReadOnlyList<string> path,
            string segment)
        {
            var result = new string[path.Count + 1];
            for (var index = 0; index < path.Count; index++)
                result[index] = path[index];
            result[path.Count] = segment;
            return result;
        }

        private static IReadOnlyList<string> Combine(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            var result = new string[left.Count + right.Count];
            for (var index = 0; index < left.Count; index++)
                result[index] = left[index];
            for (var index = 0; index < right.Count; index++)
                result[left.Count + index] = right[index];
            return result;
        }
    }
}
