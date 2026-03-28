using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal enum SymbolKind
  {
    Namespace,
    Type,
    MethodGroup,
    Method,
    Parameter,
    Local
  }

  public enum TypeKind
  {
    Error,
    U0,
    I8,
    U8,
    I16,
    U16,
    I32,
    U32,
    I64,
    U64,
    F32,
    F64,
    Char,
    String,
    Bool,
    Null,
    Array,
    Named,
    NamespacePseudo,
    MethodGroupPseudo
  }

  internal abstract class Symbol
  {
    protected Symbol(string name)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }
    public abstract SymbolKind Kind { get; }
  }

  internal sealed class NamespaceSymbol : Symbol
  {
    private readonly Dictionary<string, NamespaceSymbol> _namespaces =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeSymbol> _types =
        new(StringComparer.Ordinal);

    public override SymbolKind Kind => SymbolKind.Namespace;
    public string QualifiedName { get; }

    public NamespaceSymbol(string name, string qualifiedName = null)
        : base(name)
    {
      QualifiedName = qualifiedName ?? name;
    }

    public void AddNamespace(NamespaceSymbol namespaceSymbol)
    {
      if (namespaceSymbol == null)
        throw new ArgumentNullException(nameof(namespaceSymbol));

      _namespaces[namespaceSymbol.Name] = namespaceSymbol;
    }

    public void AddType(TypeSymbol typeSymbol)
    {
      if (typeSymbol == null)
        throw new ArgumentNullException(nameof(typeSymbol));

      _types[typeSymbol.Name] = typeSymbol;
    }

    public Symbol Lookup(string name)
    {
      if (_namespaces.TryGetValue(name, out var namespaceSymbol))
        return namespaceSymbol;

      if (_types.TryGetValue(name, out var typeSymbol))
        return typeSymbol;

      return null;
    }
  }

  internal sealed class TypeSymbol : Symbol, IEquatable<TypeSymbol>
  {
    public static readonly TypeSymbol Error =
        new(TypeKind.Error, "error", "error", false);
    public static readonly TypeSymbol U0 =
        new(TypeKind.U0, "u0", "u0", false);
    public static readonly TypeSymbol Void = U0;
    public static readonly TypeSymbol I8 =
        new(TypeKind.I8, "i8", "i8", false);
    public static readonly TypeSymbol U8 =
        new(TypeKind.U8, "u8", "u8", false);
    public static readonly TypeSymbol I16 =
        new(TypeKind.I16, "i16", "i16", false);
    public static readonly TypeSymbol U16 =
        new(TypeKind.U16, "u16", "u16", false);
    public static readonly TypeSymbol I32 =
        new(TypeKind.I32, "i32", "i32", false);
    public static readonly TypeSymbol U32 =
        new(TypeKind.U32, "u32", "u32", false);
    public static readonly TypeSymbol I64 =
        new(TypeKind.I64, "i64", "i64", false);
    public static readonly TypeSymbol U64 =
        new(TypeKind.U64, "u64", "u64", false);
    public static readonly TypeSymbol F32 =
        new(TypeKind.F32, "f32", "f32", false);
    public static readonly TypeSymbol F64 =
        new(TypeKind.F64, "f64", "f64", false);
    public static readonly TypeSymbol Char =
        new(TypeKind.Char, "char", "char", false);
    public static readonly TypeSymbol String =
        new(TypeKind.String, "string", "string", true);
    public static readonly TypeSymbol Bool =
        new(TypeKind.Bool, "bool", "bool", false);
    public static readonly TypeSymbol Null =
        new(TypeKind.Null, "null", "null", false);
    public static readonly TypeSymbol NamespacePseudoType =
        new(TypeKind.NamespacePseudo, "<namespace>", "<namespace>", false);
    public static readonly TypeSymbol MethodGroupPseudoType =
        new(TypeKind.MethodGroupPseudo, "<method-group>", "<method-group>", false);

    private readonly Dictionary<string, List<MethodSymbol>> _methods =
        new(StringComparer.Ordinal);

    public override SymbolKind Kind => SymbolKind.Type;
    public TypeKind TypeKind { get; }
    public string QualifiedName { get; }
    public TypeSymbol ElementType { get; }
    public bool IsReferenceType { get; }

    private TypeSymbol(
        TypeKind typeKind,
        string name,
        string qualifiedName,
        bool isReferenceType,
        TypeSymbol elementType = null)
        : base(name)
    {
      TypeKind = typeKind;
      QualifiedName = qualifiedName ?? name;
      IsReferenceType = isReferenceType;
      ElementType = elementType;
    }

    public static TypeSymbol CreateNamed(
        string name,
        string qualifiedName,
        bool isReferenceType = true)
    {
      return new TypeSymbol(
          TypeKind.Named,
          name,
          qualifiedName,
          isReferenceType);
    }

    public static TypeSymbol Array(TypeSymbol elementType)
    {
      if (elementType == null)
        throw new ArgumentNullException(nameof(elementType));

      return new TypeSymbol(
          TypeKind.Array,
          $"{elementType.Name}[]",
          $"{elementType.QualifiedName}[]",
          true,
          elementType);
    }

    public void AddMethod(MethodSymbol method)
    {
      if (method == null)
        throw new ArgumentNullException(nameof(method));

      if (!ReferenceEquals(method.ContainingType, this))
      {
        throw new InvalidOperationException(
            "Method must belong to the containing type it is added to.");
      }

      if (!_methods.TryGetValue(method.Name, out var methods))
      {
        methods = new List<MethodSymbol>();
        _methods.Add(method.Name, methods);
      }

      methods.Add(method);
    }

    public IReadOnlyList<MethodSymbol> GetMethods(string name)
    {
      if (_methods.TryGetValue(name, out var methods))
        return methods;

      return System.Array.Empty<MethodSymbol>();
    }

    public bool Equals(TypeSymbol other)
    {
      if (ReferenceEquals(this, other))
        return true;

      if (ReferenceEquals(other, null) || TypeKind != other.TypeKind)
        return false;

      if (TypeKind == TypeKind.Array)
        return Equals(ElementType, other.ElementType);

      if (TypeKind == TypeKind.Named)
      {
        return string.Equals(
            QualifiedName,
            other.QualifiedName,
            StringComparison.Ordinal);
      }

      return true;
    }

    public override bool Equals(object obj)
    {
      return obj is TypeSymbol other && Equals(other);
    }

    public override int GetHashCode()
    {
      unchecked
      {
        var hash = (int)TypeKind * 397;
        hash = (hash * 397) ^ (QualifiedName?.GetHashCode() ?? 0);
        hash = (hash * 397) ^ (ElementType?.GetHashCode() ?? 0);
        return hash;
      }
    }

    public static bool operator ==(TypeSymbol left, TypeSymbol right)
    {
      return Equals(left, right);
    }

    public static bool operator !=(TypeSymbol left, TypeSymbol right)
    {
      return !Equals(left, right);
    }
  }

  internal sealed class ParameterSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.Parameter;
    public TypeSymbol Type { get; }
    public int Ordinal { get; }

    public ParameterSymbol(string name, TypeSymbol type, int ordinal)
        : base(name)
    {
      Type = type ?? throw new ArgumentNullException(nameof(type));
      Ordinal = ordinal;
    }
  }

  internal sealed class LocalVariableSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.Local;
    public TypeSymbol Type { get; }
    public bool IsMutable { get; }
    public TextSpan DeclarationSpan { get; }

    public LocalVariableSymbol(
        string name,
        TypeSymbol type,
        bool isMutable,
        TextSpan declarationSpan)
        : base(name)
    {
      Type = type ?? throw new ArgumentNullException(nameof(type));
      IsMutable = isMutable;
      DeclarationSpan = declarationSpan;
    }
  }

  internal sealed class ExternBinding
  {
    public string ProviderName { get; }
    public string SignatureKey { get; }

    public ExternBinding(string providerName, string signatureKey)
    {
      ProviderName = providerName ?? throw new ArgumentNullException(nameof(providerName));
      SignatureKey = signatureKey ?? throw new ArgumentNullException(nameof(signatureKey));
    }
  }

  internal sealed class MethodSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.Method;
    public TypeSymbol ContainingType { get; }
    public IReadOnlyList<ParameterSymbol> Parameters { get; }
    public TypeSymbol ReturnType { get; }
    public bool IsStatic { get; }
    public ExternBinding ExternBinding { get; }
    public string DisplayName => $"{ContainingType.Name}.{Name}";

    public MethodSymbol(
        string name,
        TypeSymbol containingType,
        IReadOnlyList<ParameterSymbol> parameters,
        TypeSymbol returnType,
        bool isStatic,
        ExternBinding externBinding = null)
        : base(name)
    {
      ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
      Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
      ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
      IsStatic = isStatic;
      ExternBinding = externBinding;
    }
  }

  internal sealed class MethodGroupSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.MethodGroup;
    public TypeSymbol ContainingType { get; }
    public IReadOnlyList<MethodSymbol> Methods { get; }
    public string DisplayName => $"{ContainingType.Name}.{Name}";

    public MethodGroupSymbol(
        string name,
        TypeSymbol containingType,
        IReadOnlyList<MethodSymbol> methods)
        : base(name)
    {
      ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
      Methods = methods ?? throw new ArgumentNullException(nameof(methods));
    }
  }

  internal sealed class ResolvedExternMethod
  {
    public string Signature { get; }

    public ResolvedExternMethod(string signature)
    {
      Signature = signature ?? throw new ArgumentNullException(nameof(signature));
    }
  }

  internal interface IExternSignatureProvider
  {
    bool TryGetSignature(MethodSymbol method, out string signature);
    bool IsExposed(string signature);
  }

  internal sealed class UdonExternResolver : IExternSignatureProvider
  {
    public const string ProviderName = "udon";

    private readonly Dictionary<string, string> _signatureByKey;
    private readonly HashSet<string> _exposedSignatures;

    public UdonExternResolver(IReadOnlyDictionary<string, string> signatureByKey)
    {
      if (signatureByKey == null)
        throw new ArgumentNullException(nameof(signatureByKey));

      _signatureByKey = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var pair in signatureByKey)
        _signatureByKey[pair.Key] = pair.Value;

      _exposedSignatures = new HashSet<string>(StringComparer.Ordinal);
      foreach (var signature in _signatureByKey.Values)
        _exposedSignatures.Add(signature);
    }

    public bool TryGetSignature(MethodSymbol method, out string signature)
    {
      signature = null;

      if (method == null || method.ExternBinding == null)
        return false;

      if (!string.Equals(
              method.ExternBinding.ProviderName,
              ProviderName,
              StringComparison.Ordinal))
      {
        return false;
      }

      if (!_signatureByKey.TryGetValue(method.ExternBinding.SignatureKey, out signature))
        return false;

      return IsExposed(signature);
    }

    public bool IsExposed(string signature)
    {
      return !string.IsNullOrEmpty(signature) &&
             _exposedSignatures.Contains(signature);
    }
  }

  internal sealed class ExternResolver
  {
    private readonly IReadOnlyList<IExternSignatureProvider> _providers;

    public ExternResolver(IReadOnlyList<IExternSignatureProvider> providers)
    {
      _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    public bool TryResolve(MethodSymbol method, out ResolvedExternMethod resolvedMethod)
    {
      foreach (var provider in _providers)
      {
        if (!provider.TryGetSignature(method, out var signature))
          continue;

        resolvedMethod = new ResolvedExternMethod(signature);
        return true;
      }

      resolvedMethod = null;
      return false;
    }
  }

  internal sealed class SobakasuCompilationEnvironment
  {
    public NamespaceSymbol GlobalNamespace { get; }
    public ExternResolver ExternResolver { get; }

    public SobakasuCompilationEnvironment(
        NamespaceSymbol globalNamespace,
        ExternResolver externResolver)
    {
      GlobalNamespace = globalNamespace ?? throw new ArgumentNullException(nameof(globalNamespace));
      ExternResolver = externResolver ?? throw new ArgumentNullException(nameof(externResolver));
    }
  }

  internal static class SobakasuBuiltInEnvironment
  {
    public static SobakasuCompilationEnvironment Default { get; } =
        CreateDefault();

    private static SobakasuCompilationEnvironment CreateDefault()
    {
      var globalNamespace = new NamespaceSymbol("<global>", "");
      var unityEngineNamespace = new NamespaceSymbol("UnityEngine");
      globalNamespace.AddNamespace(unityEngineNamespace);

      var debugType = TypeSymbol.CreateNamed("Debug", "UnityEngine.Debug");
      globalNamespace.AddType(debugType);
      unityEngineNamespace.AddType(debugType);

      const string debugLogBindingKey = "UnityEngine.Debug.Log";
      foreach (var parameterType in new[]
               {
                 TypeSymbol.String,
                 TypeSymbol.Bool,
                 TypeSymbol.Char,
                 TypeSymbol.I8,
                 TypeSymbol.U8,
                 TypeSymbol.I16,
                 TypeSymbol.U16,
                 TypeSymbol.I32,
                 TypeSymbol.U32,
                 TypeSymbol.I64,
                 TypeSymbol.U64,
                 TypeSymbol.F32,
                 TypeSymbol.F64
               })
      {
        debugType.AddMethod(CreateMethod(
            debugType,
            "Log",
            new[] { parameterType },
            TypeSymbol.U0,
            debugLogBindingKey));
      }

      var udonExternResolver = new UdonExternResolver(
          new Dictionary<string, string>(StringComparer.Ordinal)
          {
            [debugLogBindingKey] = "UnityEngineDebug.__Log__SystemObject__SystemVoid"
          });

      return new SobakasuCompilationEnvironment(
          globalNamespace,
          new ExternResolver(new IExternSignatureProvider[] { udonExternResolver }));
    }

    private static MethodSymbol CreateMethod(
        TypeSymbol containingType,
        string name,
        IReadOnlyList<TypeSymbol> parameterTypes,
        TypeSymbol returnType,
        string externBindingKey)
    {
      var parameters = new List<ParameterSymbol>(parameterTypes.Count);
      for (var index = 0; index < parameterTypes.Count; index++)
      {
        parameters.Add(new ParameterSymbol(
            $"arg{index}",
            parameterTypes[index],
            index));
      }

      return new MethodSymbol(
          name,
          containingType,
          parameters,
          returnType,
          true,
          new ExternBinding(UdonExternResolver.ProviderName, externBindingKey));
    }
  }

  internal abstract class BoundNode
  {
  }

  internal abstract class BoundStatement : BoundNode
  {
  }

  internal abstract class BoundExpression : BoundNode
  {
    public abstract TypeSymbol Type { get; }
  }

  internal sealed class BoundErrorExpression : BoundExpression
  {
    public static readonly BoundErrorExpression Instance = new();

    public override TypeSymbol Type => TypeSymbol.Error;

    private BoundErrorExpression()
    {
    }
  }

  internal sealed class BoundProgram : BoundNode
  {
    public IReadOnlyList<BoundEventDeclaration> Events { get; }

    public BoundProgram(IReadOnlyList<BoundEventDeclaration> events)
    {
      Events = events;
    }
  }

  internal sealed class BoundEventDeclaration : BoundNode
  {
    public string Name { get; }
    public string ExportName { get; }
    public BoundBlockStatement Body { get; }

    public BoundEventDeclaration(
        string name,
        string exportName,
        BoundBlockStatement body)
    {
      Name = name;
      ExportName = exportName;
      Body = body;
    }
  }

  internal sealed class BoundBlockStatement : BoundStatement
  {
    public IReadOnlyList<BoundStatement> Statements { get; }

    public BoundBlockStatement(IReadOnlyList<BoundStatement> statements)
    {
      Statements = statements;
    }
  }

  internal sealed class BoundExpressionStatement : BoundStatement
  {
    public BoundExpression Expression { get; }

    public BoundExpressionStatement(BoundExpression expression)
    {
      Expression = expression;
    }
  }

  internal sealed class BoundVariableDeclarationStatement : BoundStatement
  {
    public LocalVariableSymbol Variable { get; }
    public BoundExpression Initializer { get; }

    public BoundVariableDeclarationStatement(
        LocalVariableSymbol variable,
        BoundExpression initializer)
    {
      Variable = variable ?? throw new ArgumentNullException(nameof(variable));
      Initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
    }
  }

  internal sealed class BoundNameExpression : BoundExpression
  {
    public string Name { get; }
    public Symbol Symbol { get; }
    public override TypeSymbol Type { get; }

    public BoundNameExpression(
        string name,
        Symbol symbol,
        TypeSymbol type)
    {
      Name = name;
      Symbol = symbol;
      Type = type;
    }
  }

  internal sealed class BoundAssignmentExpression : BoundExpression
  {
    public LocalVariableSymbol Variable { get; }
    public BoundExpression Expression { get; }
    public override TypeSymbol Type => Variable.Type;

    public BoundAssignmentExpression(
        LocalVariableSymbol variable,
        BoundExpression expression)
    {
      Variable = variable ?? throw new ArgumentNullException(nameof(variable));
      Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }
  }

  internal sealed class BoundLiteralExpression : BoundExpression
  {
    public object Value { get; }
    public override TypeSymbol Type { get; }
    public TextSpan Span { get; }

    public BoundLiteralExpression(object value, TypeSymbol type, TextSpan span)
    {
      Value = value;
      Type = type ?? throw new ArgumentNullException(nameof(type));
      Span = span;
    }
  }

  internal sealed class BoundArrayLiteralExpression : BoundExpression
  {
    public IReadOnlyList<BoundExpression> Elements { get; }
    public TypeSymbol ElementType { get; }
    public override TypeSymbol Type { get; }

    public BoundArrayLiteralExpression(
        IReadOnlyList<BoundExpression> elements,
        TypeSymbol elementType)
    {
      Elements = elements;
      ElementType = elementType;
      Type = TypeSymbol.Array(elementType);
    }
  }

  internal sealed class BoundMemberAccessExpression : BoundExpression
  {
    public BoundExpression Receiver { get; }
    public string MemberName { get; }
    public Symbol MemberSymbol { get; }
    public override TypeSymbol Type { get; }

    public BoundMemberAccessExpression(
        BoundExpression receiver,
        string memberName,
        Symbol memberSymbol,
        TypeSymbol type)
    {
      Receiver = receiver;
      MemberName = memberName;
      MemberSymbol = memberSymbol;
      Type = type;
    }
  }

  internal sealed class BoundCallExpression : BoundExpression
  {
    public BoundExpression Target { get; }
    public IReadOnlyList<BoundExpression> Arguments { get; }
    public MethodSymbol Method { get; }
    public override TypeSymbol Type { get; }

    public BoundCallExpression(
        BoundExpression target,
        IReadOnlyList<BoundExpression> arguments,
        MethodSymbol method,
        TypeSymbol type)
    {
      Target = target;
      Arguments = arguments;
      Method = method;
      Type = type;
    }
  }
}
