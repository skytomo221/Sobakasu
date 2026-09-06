using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class FunctionSymbol : Symbol, ICallableSymbol
    {
        public override SymbolKind Kind => SymbolKind.Function;
        public TypeSymbol ReturnType { get; private set; }
        public IReadOnlyList<ParameterSymbol> Parameters { get; }
        public TextSpan SourceSpan { get; }
        public TypeSymbol ContainingType { get; }
        public ParameterSymbol SelfParameter { get; }
        public bool IsStatic { get; }
        public bool IsPublic { get; }
        public bool IsOperator { get; }
        public Syntax.SyntaxKind? OperatorKind { get; }
        public string DeclaringModule { get; }
        public IReadOnlyList<TypeSymbol> GenericParameters { get; }
        public IReadOnlyList<TypeSymbol> TypeArguments { get; }
        public bool IsGenericDefinition => GenericParameters.Count > 0 && TypeArguments.Count == 0;
        public string DeclarationIdentity => string.IsNullOrEmpty(DeclaringModule)
            ? Name
            : $"{DeclaringModule}.{Name}";
        public string InternalIdentity
        {
            get
            {
                var parameterTypes = new string[Parameters.Count];
                for (var index = 0; index < Parameters.Count; index++)
                    parameterTypes[index] = Parameters[index].Type.QualifiedName;
                var signature = $"{Name}({string.Join(", ", parameterTypes)})";
                return string.IsNullOrEmpty(DeclaringModule)
                    ? signature
                    : $"{DeclaringModule}.{signature}";
            }
        }
        public string Signature
        {
            get
            {
                var parameterTypes = new string[Parameters.Count];
                for (var index = 0; index < Parameters.Count; index++)
                    parameterTypes[index] = Parameters[index].Type.Name;
                return $"{Name}({string.Join(", ", parameterTypes)})";
            }
        }
        public bool UsesExternalCallConversions => false;
        public ExternalFunctionBinding ExternalBinding { get; private set; }
        public string CanonicalPublicPath { get; private set; }
        public bool IsMethod => ContainingType != null;
        public string DisplayName => IsMethod
            ? $"{ContainingType.Name}.{Name}"
            : Name;

        public FunctionSymbol(
            string name,
            TypeSymbol returnType,
            IReadOnlyList<ParameterSymbol> parameters,
            TextSpan sourceSpan,
            TypeSymbol containingType = null,
            ParameterSymbol selfParameter = null,
            bool isStatic = false,
            bool isPublic = false,
            bool isOperator = false,
            Syntax.SyntaxKind? operatorKind = null,
            string declaringModule = null,
            IReadOnlyList<TypeSymbol> genericParameters = null,
            IReadOnlyList<TypeSymbol> typeArguments = null)
            : base(name)
        {
            ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            SourceSpan = sourceSpan;
            ContainingType = containingType;
            SelfParameter = selfParameter;
            IsStatic = isStatic;
            IsPublic = isPublic;
            IsOperator = isOperator;
            OperatorKind = operatorKind;
            DeclaringModule = declaringModule ?? string.Empty;
            GenericParameters = genericParameters ?? Array.Empty<TypeSymbol>();
            TypeArguments = typeArguments ?? Array.Empty<TypeSymbol>();
        }

        public void SetInferredReturnType(TypeSymbol returnType)
        {
            if (ReturnType != TypeSymbol.Error)
                throw new InvalidOperationException("Only an unresolved function return type can be inferred.");
            ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
        }

        public void SetExternalBinding(ExternalFunctionBinding binding)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));
            if (!ReferenceEquals(binding.SobakasuSymbol, this))
                throw new InvalidOperationException("External binding metadata must reference this function.");
            ExternalBinding = binding;
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
