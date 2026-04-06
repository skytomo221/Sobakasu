using System;
using System.Collections.Generic;
using System.Reflection;
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

    public NamespaceSymbol GetOrAddNamespace(string name)
    {
      if (_namespaces.TryGetValue(name, out var existingNamespace))
        return existingNamespace;

      var qualifiedName = string.IsNullOrEmpty(QualifiedName)
          ? name
          : $"{QualifiedName}.{name}";
      var namespaceSymbol = new NamespaceSymbol(name, qualifiedName);
      _namespaces.Add(name, namespaceSymbol);
      return namespaceSymbol;
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
    public static readonly TypeSymbol Object =
        new(TypeKind.Named, "object", "System.Object", true);
    public static readonly TypeSymbol NamespacePseudoType =
        new(TypeKind.NamespacePseudo, "<namespace>", "<namespace>", false);
    public static readonly TypeSymbol MethodGroupPseudoType =
        new(TypeKind.MethodGroupPseudo, "<method-group>", "<method-group>", false);

    private readonly Dictionary<string, MethodGroupSymbol> _methodGroups =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _unsupportedImportMembers =
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

      GetOrCreateMethodGroup(method.Name).AddMethod(method);
    }

    public void AddRejectedCandidate(string methodName, ExternCandidate candidate)
    {
      if (string.IsNullOrWhiteSpace(methodName))
        throw new ArgumentException("Method group name is required.", nameof(methodName));

      if (candidate == null)
        throw new ArgumentNullException(nameof(candidate));

      GetOrCreateMethodGroup(methodName).AddRejectedCandidate(candidate);
    }

    public void AddUnsupportedImportMember(string memberName, string reason)
    {
      if (string.IsNullOrWhiteSpace(memberName))
        throw new ArgumentException("Member name is required.", nameof(memberName));

      if (_unsupportedImportMembers.ContainsKey(memberName))
        return;

      _unsupportedImportMembers.Add(memberName, reason ?? string.Empty);
    }

    public MethodGroupSymbol GetMethodGroup(string name)
    {
      if (_methodGroups.TryGetValue(name, out var methodGroup))
        return methodGroup;

      return null;
    }

    public bool TryGetUnsupportedImportMemberReason(string memberName, out string reason)
    {
      return _unsupportedImportMembers.TryGetValue(memberName, out reason);
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

    private MethodGroupSymbol GetOrCreateMethodGroup(string name)
    {
      if (_methodGroups.TryGetValue(name, out var methodGroup))
        return methodGroup;

      methodGroup = new MethodGroupSymbol(name, this);
      _methodGroups.Add(name, methodGroup);
      return methodGroup;
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

  internal class MethodSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.Method;
    public TypeSymbol ContainingType { get; }
    public IReadOnlyList<ParameterSymbol> Parameters { get; }
    public TypeSymbol ReturnType { get; }
    public bool IsStatic { get; }
    public virtual string ExternSignature => null;
    public string DisplayName => $"{ContainingType.Name}.{Name}";

    public MethodSymbol(
        string name,
        TypeSymbol containingType,
        IReadOnlyList<ParameterSymbol> parameters,
        TypeSymbol returnType,
        bool isStatic)
        : base(name)
    {
      ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
      Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
      ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
      IsStatic = isStatic;
    }
  }

  internal sealed class ExternMethodSymbol : MethodSymbol
  {
    public MethodInfo MethodInfo { get; }
    public override string ExternSignature { get; }

    public ExternMethodSymbol(
        string name,
        TypeSymbol containingType,
        IReadOnlyList<ParameterSymbol> parameters,
        TypeSymbol returnType,
        MethodInfo methodInfo,
        string externSignature)
        : base(name, containingType, parameters, returnType, true)
    {
      MethodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
      ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature));
    }
  }

  internal sealed class MethodGroupSymbol : Symbol
  {
    private readonly List<MethodSymbol> _methods = new();
    private readonly List<ExternCandidate> _rejectedCandidates = new();

    public override SymbolKind Kind => SymbolKind.MethodGroup;
    public TypeSymbol ContainingType { get; }
    public IReadOnlyList<MethodSymbol> Methods => _methods;
    public IReadOnlyList<ExternCandidate> RejectedCandidates => _rejectedCandidates;
    public string DisplayName => $"{ContainingType.Name}.{Name}";

    public MethodGroupSymbol(string name, TypeSymbol containingType)
        : base(name)
    {
      ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
    }

    public void AddMethod(MethodSymbol method)
    {
      if (method == null)
        throw new ArgumentNullException(nameof(method));

      _methods.Add(method);
    }

    public void AddRejectedCandidate(ExternCandidate candidate)
    {
      if (candidate == null)
        throw new ArgumentNullException(nameof(candidate));

      _rejectedCandidates.Add(candidate);
    }
  }

  internal sealed class SobakasuCompilationEnvironment
  {
    public NamespaceSymbol GlobalNamespace { get; }
    public ExternCatalog ExternCatalog { get; }
    public IReadOnlyDictionary<string, Symbol> CompatibilitySymbols { get; }

    public SobakasuCompilationEnvironment(
        ExternCatalog externCatalog,
        IReadOnlyDictionary<string, Symbol> compatibilitySymbols)
    {
      ExternCatalog = externCatalog ?? throw new ArgumentNullException(nameof(externCatalog));
      GlobalNamespace = externCatalog.GlobalNamespace;
      CompatibilitySymbols = compatibilitySymbols ??
          throw new ArgumentNullException(nameof(compatibilitySymbols));
    }

    public bool TryLookupCompatibilitySymbol(string name, out Symbol symbol)
    {
      return CompatibilitySymbols.TryGetValue(name, out symbol);
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
