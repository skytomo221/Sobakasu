using System;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ConstantSymbol : Symbol
    {
        public override SymbolKind Kind => SymbolKind.Constant;
        public TypeSymbol Type { get; private set; }
        public object ConstantValue { get; private set; }
        public bool HasConstantValue { get; private set; }
        public bool IsPublic { get; }
        public string DeclaringModule { get; }
        public TextSpan DeclarationSpan { get; }
        public TextSpan InitializerSpan { get; private set; }
        public string DeclarationIdentity => string.IsNullOrEmpty(DeclaringModule)
            ? Name
            : $"{DeclaringModule}.{Name}";
        public string CanonicalPublicPath { get; private set; }

        public ConstantSymbol(
            string name,
            bool isPublic,
            string declaringModule,
            TextSpan declarationSpan)
            : base(name)
        {
            Type = TypeSymbol.Error;
            IsPublic = isPublic;
            DeclaringModule = declaringModule ?? string.Empty;
            DeclarationSpan = declarationSpan;
            InitializerSpan = declarationSpan;
        }

        public void SetBinding(
            TypeSymbol type,
            object constantValue,
            bool hasConstantValue,
            TextSpan initializerSpan)
        {
            Type = type ?? TypeSymbol.Error;
            ConstantValue = constantValue;
            HasConstantValue = hasConstantValue;
            InitializerSpan = initializerSpan;
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
