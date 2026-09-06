using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal enum EnumVariantKind
    {
        Unit,
        Tuple,
        Struct
    }

    internal sealed class EnumVariantSymbol : Symbol
    {
        private readonly Dictionary<string, AggregateFieldSymbol> _fieldsByName =
            new(StringComparer.Ordinal);

        public override SymbolKind Kind => SymbolKind.EnumVariant;
        public TypeSymbol ContainingType { get; }
        public EnumVariantKind VariantKind { get; }
        public int Tag { get; }
        public IReadOnlyList<AggregateFieldSymbol> Fields { get; }
        public TextSpan DeclarationSpan { get; }
        public string DeclarationIdentity => $"{ContainingType.DeclarationIdentity}.{Name}";
        public string CanonicalPublicPath { get; private set; }
        public string ExternalMemberName { get; }

        public EnumVariantSymbol(
            string name,
            TypeSymbol containingType,
            EnumVariantKind variantKind,
            int tag,
            IReadOnlyList<AggregateFieldSymbol> fields,
            TextSpan declarationSpan,
            string externalMemberName = null)
            : base(name)
        {
            ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
            VariantKind = variantKind;
            Tag = tag;
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
            DeclarationSpan = declarationSpan;
            ExternalMemberName = externalMemberName;
            foreach (var field in fields)
                _fieldsByName[field.Name] = field;
        }

        public bool TryGetField(string name, out AggregateFieldSymbol field)
        {
            return _fieldsByName.TryGetValue(name, out field);
        }

        public void RegisterPublicPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            if (string.IsNullOrEmpty(CanonicalPublicPath) ||
                path.Split('.').Length < CanonicalPublicPath.Split('.').Length ||
                path.Split('.').Length == CanonicalPublicPath.Split('.').Length &&
                string.CompareOrdinal(path, CanonicalPublicPath) < 0)
            {
                CanonicalPublicPath = path;
            }
        }
    }
}
