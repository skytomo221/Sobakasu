using System;
using System.Collections.Generic;
using System.Reflection;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal enum SymbolKind
  {
    Module,
    Namespace,
    Type,
    MethodGroup,
    Method,
    Event,
    NetworkReceive,
    Function,
    Parameter,
    Local,
    State,
    Constant,
    AggregateField,
    EnumVariant
  }

  public enum TypeKind
  {
    Error,
    Never,
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
    ModulePseudo,
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

  internal sealed class ModuleSymbol : Symbol
  {
    private readonly Dictionary<string, ModuleSymbol> _children =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Symbol> _declarations =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, Symbol> _exports =
        new(StringComparer.Ordinal);

    public override SymbolKind Kind => SymbolKind.Module;
    public StandardLibraryModule SourceModule { get; }
    public string QualifiedName => SourceModule.LogicalName;
    public ModuleSymbol Parent { get; private set; }
    public bool IsPublic => SourceModule.IsPublic;
    public bool IsConnected => SourceModule.IsConnected;
    public bool IsPrelude => SourceModule.IsPrelude;
    public string CanonicalPublicPath { get; private set; }
    public IReadOnlyDictionary<string, ModuleSymbol> Children => _children;
    public IReadOnlyDictionary<string, Symbol> Exports => _exports;

    public ModuleSymbol(StandardLibraryModule sourceModule)
        : base(sourceModule?.SimpleName ?? string.Empty)
    {
      SourceModule = sourceModule ?? throw new ArgumentNullException(nameof(sourceModule));
      if (sourceModule.IsRoot)
        CanonicalPublicPath = sourceModule.LogicalName;
    }

    public void AttachChild(ModuleSymbol child)
    {
      if (child == null)
        throw new ArgumentNullException(nameof(child));

      child.Parent = this;
      _children[child.Name] = child;
      if (child.IsPublic)
      {
        _exports[child.Name] = child;
        if (!string.IsNullOrEmpty(CanonicalPublicPath))
          child.RegisterPublicPath($"{CanonicalPublicPath}.{child.Name}");
      }
    }

    public bool TryDeclare(string name, Symbol symbol)
    {
      if (_declarations.ContainsKey(name))
        return false;
      _declarations.Add(name, symbol);
      return true;
    }

    public bool TryExport(string name, Symbol symbol, out Symbol existing)
    {
      if (_exports.TryGetValue(name, out existing))
        return ReferenceEquals(existing, symbol);

      _exports.Add(name, symbol);
      return true;
    }

    public Symbol LookupDeclared(string name)
    {
      if (_declarations.TryGetValue(name, out var declaration))
        return declaration;
      if (_children.TryGetValue(name, out var child))
        return child;
      return null;
    }

    public Symbol LookupExport(string name)
    {
      return _exports.TryGetValue(name, out var symbol) ? symbol : null;
    }

    public void RegisterPublicPath(string path)
    {
      if (string.IsNullOrEmpty(path))
        return;

      if (string.IsNullOrEmpty(CanonicalPublicPath) ||
          IsBetterPublicPath(path, CanonicalPublicPath))
      {
        CanonicalPublicPath = path;
      }
    }

    private static bool IsBetterPublicPath(string candidate, string current)
    {
      var candidateSegments = candidate.Split('.').Length;
      var currentSegments = current.Split('.').Length;
      return candidateSegments < currentSegments ||
          candidateSegments == currentSegments &&
          string.CompareOrdinal(candidate, current) < 0;
    }
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
    private static readonly Dictionary<TypeSymbol, TypeSymbol> ArrayTypes = new();
    private static readonly object ArrayTypesGate = new();
    public static readonly TypeSymbol Error =
        new(TypeKind.Error, "error", "error", false);
    public static readonly TypeSymbol Never =
        new(TypeKind.Never, "<never>", "<never>", false);
    public static readonly TypeSymbol U0 =
        new(TypeKind.U0, "u0", "u0", false, runtimeQualifiedName: "System.Void", isBuiltIn: true);
    public static readonly TypeSymbol Void = U0;
    public static readonly TypeSymbol I8 =
        new(TypeKind.I8, "i8", "i8", false, runtimeQualifiedName: "System.SByte", isBuiltIn: true);
    public static readonly TypeSymbol U8 =
        new(TypeKind.U8, "u8", "u8", false, runtimeQualifiedName: "System.Byte", isBuiltIn: true);
    public static readonly TypeSymbol I16 =
        new(TypeKind.I16, "i16", "i16", false, runtimeQualifiedName: "System.Int16", isBuiltIn: true);
    public static readonly TypeSymbol U16 =
        new(TypeKind.U16, "u16", "u16", false, runtimeQualifiedName: "System.UInt16", isBuiltIn: true);
    public static readonly TypeSymbol I32 =
        new(TypeKind.I32, "i32", "i32", false, runtimeQualifiedName: "System.Int32", isBuiltIn: true);
    public static readonly TypeSymbol U32 =
        new(TypeKind.U32, "u32", "u32", false, runtimeQualifiedName: "System.UInt32", isBuiltIn: true);
    public static readonly TypeSymbol I64 =
        new(TypeKind.I64, "i64", "i64", false, runtimeQualifiedName: "System.Int64", isBuiltIn: true);
    public static readonly TypeSymbol U64 =
        new(TypeKind.U64, "u64", "u64", false, runtimeQualifiedName: "System.UInt64", isBuiltIn: true);
    public static readonly TypeSymbol F32 =
        new(TypeKind.F32, "f32", "f32", false, runtimeQualifiedName: "System.Single", isBuiltIn: true);
    public static readonly TypeSymbol F64 =
        new(TypeKind.F64, "f64", "f64", false, runtimeQualifiedName: "System.Double", isBuiltIn: true);
    public static readonly TypeSymbol Char =
        new(TypeKind.Char, "char", "char", false, runtimeQualifiedName: "System.Char", isBuiltIn: true);
    public static readonly TypeSymbol String =
        new(TypeKind.String, "string", "string", true, runtimeQualifiedName: "System.String", isBuiltIn: true);
    public static readonly TypeSymbol Bool =
        new(TypeKind.Bool, "bool", "bool", false, runtimeQualifiedName: "System.Boolean", isBuiltIn: true);
    public static readonly TypeSymbol Null =
        new(TypeKind.Null, "null", "null", false);
    public static readonly TypeSymbol Object =
        new(
            TypeKind.Named,
            "object",
            "System.Object",
            true,
            runtimeQualifiedName: "System.Object",
            isBuiltIn: true);
    public static readonly TypeSymbol NamespacePseudoType =
        new(TypeKind.NamespacePseudo, "<namespace>", "<namespace>", false);
    public static readonly TypeSymbol ModulePseudoType =
        new(TypeKind.ModulePseudo, "<module>", "<module>", false);
    public static readonly TypeSymbol MethodGroupPseudoType =
        new(TypeKind.MethodGroupPseudo, "<method-group>", "<method-group>", false);

    private readonly Dictionary<string, MethodGroupSymbol> _methodGroups =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _unsupportedImportMembers =
        new(StringComparer.Ordinal);
    private readonly List<AggregateFieldSymbol> _aggregateFields = new();
    private readonly List<EnumVariantSymbol> _enumVariants = new();
    private readonly Dictionary<string, AggregateFieldSymbol> _aggregateFieldsByName =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, EnumVariantSymbol> _enumVariantsByName =
        new(StringComparer.Ordinal);
    private readonly List<TypeSymbol> _genericParameters = new();
    private readonly Dictionary<TypeArgumentListKey, TypeSymbol> _constructedGenericTypes =
        new();
    private bool _aggregateMembersInitialized;
    private bool _constructedMembersInitialized;

    public override SymbolKind Kind => SymbolKind.Type;
    public TypeKind TypeKind { get; }
    public string QualifiedName { get; }
    public string RuntimeQualifiedName { get; }
    public TypeSymbol ElementType { get; }
    public bool IsReferenceType { get; }
    public bool IsBuiltIn { get; }
    public bool IsExternalBinding { get; }
    public bool IsPublic { get; }
    public string DeclaringModule { get; }
    public UserAggregateKind? AggregateKind { get; }
    public bool IsAggregate => AggregateKind.HasValue;
    public bool IsGenericParameter { get; }
    public object GenericParameterOwner { get; }
    public int GenericParameterOrdinal { get; }
    public IReadOnlyList<TypeSymbol> GenericParameters => _genericParameters;
    public TypeSymbol GenericDefinition { get; }
    public IReadOnlyList<TypeSymbol> TypeArguments { get; }
    public bool IsGenericDefinition => GenericDefinition == null &&
        _genericParameters.Count > 0;
    public bool IsConstructedGenericType => GenericDefinition != null;
    public bool ContainsGenericParameters
    {
      get
      {
        if (IsGenericParameter || IsGenericDefinition)
          return true;
        if (TypeKind == TypeKind.Array)
          return ElementType?.ContainsGenericParameters == true;
        if (!IsConstructedGenericType)
          return false;
        foreach (var argument in TypeArguments)
        {
          if (argument.ContainsGenericParameters)
            return true;
        }
        return false;
      }
    }
    public IReadOnlyCollection<TypeSymbol> ConstructedGenericTypes =>
        _constructedGenericTypes.Values;
    public IReadOnlyList<AggregateFieldSymbol> AggregateFields => _aggregateFields;
    public IReadOnlyList<EnumVariantSymbol> EnumVariants => _enumVariants;
    public string DeclarationIdentity => string.IsNullOrEmpty(DeclaringModule)
        ? Name
        : $"{DeclaringModule}.{Name}";
    public string CanonicalPublicPath { get; private set; }

    private TypeSymbol(
        TypeKind typeKind,
        string name,
        string qualifiedName,
        bool isReferenceType,
        TypeSymbol elementType = null,
        string runtimeQualifiedName = null,
        bool isBuiltIn = false,
        bool isExternalBinding = false,
        bool isPublic = true,
        string declaringModule = null,
        UserAggregateKind? aggregateKind = null,
        bool isGenericParameter = false,
        object genericParameterOwner = null,
        int genericParameterOrdinal = -1,
        TypeSymbol genericDefinition = null,
        IReadOnlyList<TypeSymbol> typeArguments = null)
        : base(name)
    {
      TypeKind = typeKind;
      QualifiedName = qualifiedName ?? name;
      IsReferenceType = isReferenceType;
      ElementType = elementType;
      RuntimeQualifiedName = runtimeQualifiedName ?? qualifiedName ?? name;
      IsBuiltIn = isBuiltIn;
      IsExternalBinding = isExternalBinding;
      IsPublic = isPublic;
      DeclaringModule = declaringModule ?? string.Empty;
      AggregateKind = aggregateKind;
      IsGenericParameter = isGenericParameter;
      GenericParameterOwner = genericParameterOwner;
      GenericParameterOrdinal = genericParameterOrdinal;
      GenericDefinition = genericDefinition;
      TypeArguments = typeArguments ?? System.Array.Empty<TypeSymbol>();
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

    public static TypeSymbol CreateExternalBinding(
        string name,
        string qualifiedName,
        TypeSymbol runtimeType,
        bool isPublic,
        string declaringModule)
    {
      if (runtimeType == null)
        throw new ArgumentNullException(nameof(runtimeType));

      return new TypeSymbol(
          TypeKind.Named,
          name,
          qualifiedName,
          runtimeType.IsReferenceType,
          runtimeQualifiedName: runtimeType.RuntimeQualifiedName,
          isExternalBinding: true,
          isPublic: isPublic,
          declaringModule: declaringModule);
    }

    public static TypeSymbol CreateAggregate(
        string name,
        string qualifiedName,
        UserAggregateKind aggregateKind,
        bool isPublic,
        string declaringModule)
    {
      return new TypeSymbol(
          TypeKind.Named,
          name,
          qualifiedName,
          false,
          runtimeQualifiedName: string.Empty,
          isPublic: isPublic,
          declaringModule: declaringModule,
          aggregateKind: aggregateKind);
    }

    public static TypeSymbol CreateGenericParameter(
        string name,
        object declarationIdentity,
        int ordinal,
        string ownerDisplayName)
    {
      if (declarationIdentity == null)
        throw new ArgumentNullException(nameof(declarationIdentity));

      return new TypeSymbol(
          TypeKind.Named,
          name,
          $"{ownerDisplayName}.{name}#{ordinal}",
          false,
          runtimeQualifiedName: string.Empty,
          isPublic: false,
          isGenericParameter: true,
          genericParameterOwner: declarationIdentity,
          genericParameterOrdinal: ordinal);
    }

    public void SetGenericParameters(IReadOnlyList<TypeSymbol> parameters)
    {
      if (GenericDefinition != null || IsGenericParameter)
        throw new InvalidOperationException("Only a generic definition can declare parameters.");

      _genericParameters.Clear();
      if (parameters == null)
        return;
      foreach (var parameter in parameters)
        _genericParameters.Add(parameter);
    }

    public TypeSymbol Construct(IReadOnlyList<TypeSymbol> typeArguments)
    {
      if (!IsGenericDefinition)
        throw new InvalidOperationException($"Type '{Name}' is not a generic definition.");
      if (typeArguments == null)
        throw new ArgumentNullException(nameof(typeArguments));
      if (typeArguments.Count != _genericParameters.Count)
        throw new ArgumentException("Generic arity does not match.", nameof(typeArguments));

      var copiedArguments = new TypeSymbol[typeArguments.Count];
      for (var index = 0; index < copiedArguments.Length; index++)
        copiedArguments[index] = typeArguments[index];
      var key = new TypeArgumentListKey(copiedArguments);
      if (_constructedGenericTypes.TryGetValue(key, out var existing))
        return existing;

      var sourceName = $"{Name}<{string.Join(", ", GetTypeNames(copiedArguments))}>";
      var qualifiedName = $"{QualifiedName}<{string.Join(", ", GetQualifiedTypeNames(copiedArguments))}>";
      var constructed = new TypeSymbol(
          TypeKind.Named,
          sourceName,
          qualifiedName,
          false,
          runtimeQualifiedName: string.Empty,
          isPublic: IsPublic,
          declaringModule: DeclaringModule,
          aggregateKind: AggregateKind,
          genericDefinition: this,
          typeArguments: copiedArguments);
      _constructedGenericTypes.Add(key, constructed);
      constructed.InitializeConstructedMembers();
      return constructed;
    }

    public static TypeSymbol Substitute(
        TypeSymbol type,
        IReadOnlyDictionary<TypeSymbol, TypeSymbol> substitutions)
    {
      if (type == null || substitutions == null || substitutions.Count == 0)
        return type;
      if (type.IsGenericParameter && substitutions.TryGetValue(type, out var replacement))
        return replacement;
      if (type.TypeKind == TypeKind.Array)
      {
        var element = Substitute(type.ElementType, substitutions);
        return ReferenceEquals(element, type.ElementType) ? type : Array(element);
      }
      if (!type.IsConstructedGenericType)
        return type;

      var changed = false;
      var arguments = new TypeSymbol[type.TypeArguments.Count];
      for (var index = 0; index < arguments.Length; index++)
      {
        arguments[index] = Substitute(type.TypeArguments[index], substitutions);
        changed |= !ReferenceEquals(arguments[index], type.TypeArguments[index]);
      }
      return changed ? type.GenericDefinition.Construct(arguments) : type;
    }

    public void SetAggregateFields(IReadOnlyList<AggregateFieldSymbol> fields)
    {
      if (!IsAggregate || AggregateKind != UserAggregateKind.Struct)
        throw new InvalidOperationException("Only struct types have direct fields.");

      _aggregateFields.Clear();
      _aggregateFieldsByName.Clear();
      foreach (var field in fields)
      {
        _aggregateFields.Add(field);
        _aggregateFieldsByName[field.Name] = field;
      }
      _aggregateMembersInitialized = true;
      RefreshConstructedMembers();
    }

    public void SetEnumVariants(IReadOnlyList<EnumVariantSymbol> variants)
    {
      if (!IsAggregate || AggregateKind != UserAggregateKind.Enum)
        throw new InvalidOperationException("Only enum types have variants.");

      _enumVariants.Clear();
      _enumVariantsByName.Clear();
      foreach (var variant in variants)
      {
        _enumVariants.Add(variant);
        _enumVariantsByName[variant.Name] = variant;
      }
      _aggregateMembersInitialized = true;
      RefreshConstructedMembers();
    }

    public bool TryGetAggregateField(string name, out AggregateFieldSymbol field)
    {
      return _aggregateFieldsByName.TryGetValue(name, out field);
    }

    public bool TryGetEnumVariant(string name, out EnumVariantSymbol variant)
    {
      return _enumVariantsByName.TryGetValue(name, out variant);
    }

    public static TypeSymbol Array(TypeSymbol elementType)
    {
      if (elementType == null)
        throw new ArgumentNullException(nameof(elementType));

      lock (ArrayTypesGate)
      {
        if (ArrayTypes.TryGetValue(elementType, out var existing))
          return existing;

        var arrayType = new TypeSymbol(
            TypeKind.Array,
            $"[{elementType.Name}]",
            $"[{elementType.QualifiedName}]",
            true,
            elementType,
            runtimeQualifiedName: $"{elementType.RuntimeQualifiedName}[]");
        ArrayTypes.Add(elementType, arrayType);
        return arrayType;
      }
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

    private void RefreshConstructedMembers()
    {
      if (!IsGenericDefinition || _constructedGenericTypes.Count == 0)
        return;

      var constructedTypes = new List<TypeSymbol>(_constructedGenericTypes.Values);
      foreach (var constructed in constructedTypes)
        constructed.InitializeConstructedMembers();
    }

    private void InitializeConstructedMembers()
    {
      if (!IsConstructedGenericType ||
          _constructedMembersInitialized ||
          !GenericDefinition._aggregateMembersInitialized)
      {
        return;
      }

      var substitutions = new Dictionary<TypeSymbol, TypeSymbol>();
      for (var index = 0; index < GenericDefinition.GenericParameters.Count; index++)
      {
        substitutions.Add(
            GenericDefinition.GenericParameters[index],
            TypeArguments[index]);
      }

      if (AggregateKind == UserAggregateKind.Struct)
      {
        var fields = new List<AggregateFieldSymbol>();
        foreach (var field in GenericDefinition.AggregateFields)
        {
          fields.Add(new AggregateFieldSymbol(
              field.Name,
              this,
              Substitute(field.Type, substitutions),
              field.Ordinal,
              field.DeclarationSpan));
        }
        SetConstructedFields(fields);
      }
      else if (AggregateKind == UserAggregateKind.Enum)
      {
        var variants = new List<EnumVariantSymbol>();
        foreach (var variant in GenericDefinition.EnumVariants)
        {
          var fields = new List<AggregateFieldSymbol>();
          foreach (var field in variant.Fields)
          {
            fields.Add(new AggregateFieldSymbol(
                field.Name,
                this,
                Substitute(field.Type, substitutions),
                field.Ordinal,
                field.DeclarationSpan));
          }
          variants.Add(new EnumVariantSymbol(
              variant.Name,
              this,
              variant.VariantKind,
              variant.Tag,
              fields,
              variant.DeclarationSpan));
        }
        SetConstructedVariants(variants);
      }

      _constructedMembersInitialized = true;
    }

    private void SetConstructedFields(IReadOnlyList<AggregateFieldSymbol> fields)
    {
      _aggregateFields.Clear();
      _aggregateFieldsByName.Clear();
      foreach (var field in fields)
      {
        _aggregateFields.Add(field);
        _aggregateFieldsByName[field.Name] = field;
      }
    }

    private void SetConstructedVariants(IReadOnlyList<EnumVariantSymbol> variants)
    {
      _enumVariants.Clear();
      _enumVariantsByName.Clear();
      foreach (var variant in variants)
      {
        _enumVariants.Add(variant);
        _enumVariantsByName[variant.Name] = variant;
      }
    }

    private static IEnumerable<string> GetTypeNames(IReadOnlyList<TypeSymbol> types)
    {
      foreach (var type in types)
        yield return type.Name;
    }

    private static IEnumerable<string> GetQualifiedTypeNames(IReadOnlyList<TypeSymbol> types)
    {
      foreach (var type in types)
        yield return type.QualifiedName;
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

      if (IsGenericParameter || other.IsGenericParameter)
        return false;

      if (IsAggregate || other.IsAggregate)
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

    private sealed class TypeArgumentListKey : IEquatable<TypeArgumentListKey>
    {
      private readonly IReadOnlyList<TypeSymbol> _arguments;

      public TypeArgumentListKey(IReadOnlyList<TypeSymbol> arguments)
      {
        _arguments = arguments;
      }

      public bool Equals(TypeArgumentListKey other)
      {
        if (ReferenceEquals(this, other))
          return true;
        if (other == null || _arguments.Count != other._arguments.Count)
          return false;
        for (var index = 0; index < _arguments.Count; index++)
        {
          if (_arguments[index] != other._arguments[index])
            return false;
        }
        return true;
      }

      public override bool Equals(object obj)
      {
        return obj is TypeArgumentListKey other && Equals(other);
      }

      public override int GetHashCode()
      {
        unchecked
        {
          var hash = 17;
          foreach (var argument in _arguments)
            hash = hash * 31 + (argument?.GetHashCode() ?? 0);
          return hash;
        }
      }
    }
  }

  internal enum UserAggregateKind
  {
    Struct,
    Enum
  }

  internal enum EnumVariantKind
  {
    Unit,
    Tuple,
    Struct
  }

  internal sealed class AggregateFieldSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.AggregateField;
    public TypeSymbol ContainingType { get; }
    public TypeSymbol Type { get; }
    public int Ordinal { get; }
    public TextSpan DeclarationSpan { get; }

    public AggregateFieldSymbol(
        string name,
        TypeSymbol containingType,
        TypeSymbol type,
        int ordinal,
        TextSpan declarationSpan)
        : base(name)
    {
      ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
      Type = type ?? throw new ArgumentNullException(nameof(type));
      Ordinal = ordinal;
      DeclarationSpan = declarationSpan;
    }
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

    public EnumVariantSymbol(
        string name,
        TypeSymbol containingType,
        EnumVariantKind variantKind,
        int tag,
        IReadOnlyList<AggregateFieldSymbol> fields,
        TextSpan declarationSpan)
        : base(name)
    {
      ContainingType = containingType ?? throw new ArgumentNullException(nameof(containingType));
      VariantKind = variantKind;
      Tag = tag;
      Fields = fields ?? throw new ArgumentNullException(nameof(fields));
      DeclarationSpan = declarationSpan;
      foreach (var field in fields)
        _fieldsByName[field.Name] = field;
    }

    public bool TryGetField(string name, out AggregateFieldSymbol field)
    {
      return _fieldsByName.TryGetValue(name, out field);
    }
  }

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

      if (type.TypeKind == TypeKind.Array && type.ElementType?.IsAggregate == true)
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

      if (!type.IsAggregate)
      {
        leaves.Add(new AggregateLeafDescriptor(type, path));
        return;
      }

      if (!visiting.Add(type))
        return;

      if (type.AggregateKind == UserAggregateKind.Struct)
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

  internal sealed class AggregateConstantValue
  {
    public TypeSymbol Type { get; }
    public IReadOnlyList<object> Leaves { get; }

    public AggregateConstantValue(TypeSymbol type, IReadOnlyList<object> leaves)
    {
      Type = type ?? throw new ArgumentNullException(nameof(type));
      Leaves = leaves ?? throw new ArgumentNullException(nameof(leaves));
    }
  }

  internal sealed class ParameterSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.Parameter;
    public TypeSymbol Type { get; }
    public int Ordinal { get; }
    public string UdonStorageName { get; }
    public TextSpan? DeclarationSpan { get; }

    public ParameterSymbol(
        string name,
        TypeSymbol type,
        int ordinal,
        string udonStorageName = null,
        TextSpan? declarationSpan = null)
        : base(name)
    {
      Type = type ?? throw new ArgumentNullException(nameof(type));
      Ordinal = ordinal;
      UdonStorageName = udonStorageName ?? name;
      DeclarationSpan = declarationSpan;
    }
  }

  internal sealed class BoundEventSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.Event;
    public string SourceName { get; }
    public string UdonName { get; }
    public TypeSymbol ReturnType { get; }
    public IReadOnlyList<ParameterSymbol> Parameters { get; }
    public EventCategory Category { get; }
    public string Requirement { get; }
    public EventSupportLevel SupportLevel { get; }
    public TextSpan SourceSpan { get; }
    public string ReturnValueStorageName { get; }

    public BoundEventSymbol(
        string sourceName,
        string udonName,
        TypeSymbol returnType,
        IReadOnlyList<ParameterSymbol> parameters,
        EventCategory category,
        string requirement,
        EventSupportLevel supportLevel,
        TextSpan sourceSpan,
        string returnValueStorageName)
        : base(sourceName)
    {
      SourceName = sourceName ?? throw new ArgumentNullException(nameof(sourceName));
      UdonName = udonName ?? throw new ArgumentNullException(nameof(udonName));
      ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
      Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
      Category = category;
      Requirement = requirement;
      SupportLevel = supportLevel;
      SourceSpan = sourceSpan;
      ReturnValueStorageName = returnValueStorageName;
    }
  }

  internal sealed class NetworkReceivePhysicalParameter
  {
    public ParameterSymbol LogicalParameter { get; }
    public ParameterSymbol PhysicalParameter { get; }
    public IReadOnlyList<string> Path { get; }

    public NetworkReceivePhysicalParameter(
        ParameterSymbol logicalParameter,
        ParameterSymbol physicalParameter,
        IReadOnlyList<string> path)
    {
      LogicalParameter = logicalParameter ??
          throw new ArgumentNullException(nameof(logicalParameter));
      PhysicalParameter = physicalParameter ??
          throw new ArgumentNullException(nameof(physicalParameter));
      Path = path ?? Array.Empty<string>();
    }
  }

  internal sealed class NetworkReceiveSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.NetworkReceive;
    public string ExportName { get; }
    public IReadOnlyList<ParameterSymbol> Parameters { get; }
    public IReadOnlyList<NetworkReceivePhysicalParameter> PhysicalParameters { get; }
    public TextSpan SourceSpan { get; }

    public NetworkReceiveSymbol(
        string name,
        string exportName,
        IReadOnlyList<ParameterSymbol> parameters,
        IReadOnlyList<NetworkReceivePhysicalParameter> physicalParameters,
        TextSpan sourceSpan)
        : base(name)
    {
      ExportName = exportName ?? throw new ArgumentNullException(nameof(exportName));
      Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
      PhysicalParameters = physicalParameters ??
          throw new ArgumentNullException(nameof(physicalParameters));
      SourceSpan = sourceSpan;
    }
  }

  internal sealed class FunctionSymbol : Symbol
  {
    public override SymbolKind Kind => SymbolKind.Function;
    public TypeSymbol ReturnType { get; }
    public IReadOnlyList<ParameterSymbol> Parameters { get; }
    public TextSpan SourceSpan { get; }
    public TypeSymbol ContainingType { get; }
    public ParameterSymbol SelfParameter { get; }
    public bool IsStatic { get; }
    public bool IsPublic { get; }
    public bool IsOperator { get; }
    public Syntax.SyntaxKind? OperatorKind { get; }
    public string DeclaringModule { get; }
    public string DeclarationIdentity => string.IsNullOrEmpty(DeclaringModule)
        ? Name
        : $"{DeclaringModule}.{Name}";
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
        string declaringModule = null)
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

  internal abstract class VariableSymbol : Symbol
  {
    protected VariableSymbol(
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

    public TypeSymbol Type { get; }
    public bool IsMutable { get; }
    public TextSpan DeclarationSpan { get; }
  }

  internal sealed class LocalVariableSymbol : VariableSymbol
  {
    public override SymbolKind Kind => SymbolKind.Local;

    public LocalVariableSymbol(
        string name,
        TypeSymbol type,
        bool isMutable,
        TextSpan declarationSpan)
        : base(name, type, isMutable, declarationSpan)
    {
    }
  }

  internal enum StateSynchronizationMode
  {
    None,
    Linear,
    Smooth
  }

  internal static class StateSynchronizationCompatibility
  {
    private static readonly HashSet<string> NoneNamedTypes = new()
    {
      "UnityEngine.Color",
      "UnityEngine.Color32",
      "UnityEngine.Vector2",
      "UnityEngine.Vector3",
      "UnityEngine.Vector4",
      "UnityEngine.Quaternion",
      "VRC.SDKBase.VRCUrl"
    };

    private static readonly HashSet<string> LinearNamedTypes = new()
    {
      "UnityEngine.Color",
      "UnityEngine.Color32",
      "UnityEngine.Vector2",
      "UnityEngine.Vector3",
      "UnityEngine.Quaternion"
    };

    private static readonly HashSet<string> SmoothNamedTypes = new()
    {
      "UnityEngine.Vector2",
      "UnityEngine.Vector3",
      "UnityEngine.Quaternion"
    };

    public static bool IsSupported(
        TypeSymbol type,
        StateSynchronizationMode mode)
    {
      if (type == null || type == TypeSymbol.Error)
        return false;

      return mode switch
      {
        StateSynchronizationMode.None => IsNoneSupported(type),
        StateSynchronizationMode.Linear => IsInterpolatedNumeric(type) ||
            IsNamed(type, LinearNamedTypes),
        StateSynchronizationMode.Smooth => IsInterpolatedNumeric(type) ||
            IsNamed(type, SmoothNamedTypes),
        _ => false
      };
    }

    public static string GetSourceName(StateSynchronizationMode mode)
    {
      return mode switch
      {
        StateSynchronizationMode.None => "none",
        StateSynchronizationMode.Linear => "linear",
        StateSynchronizationMode.Smooth => "smooth",
        _ => "unknown"
      };
    }

    private static bool IsNoneSupported(TypeSymbol type)
    {
      if (IsPrimitiveSyncType(type) || IsNamed(type, NoneNamedTypes))
        return true;

      return type.TypeKind == TypeKind.Array &&
          (IsPrimitiveSyncType(type.ElementType) || IsNamed(type.ElementType, NoneNamedTypes));
    }

    private static bool IsPrimitiveSyncType(TypeSymbol type)
    {
      return type.TypeKind is TypeKind.Bool or
          TypeKind.Char or
          TypeKind.I8 or
          TypeKind.U8 or
          TypeKind.I16 or
          TypeKind.U16 or
          TypeKind.I32 or
          TypeKind.U32 or
          TypeKind.I64 or
          TypeKind.U64 or
          TypeKind.F32 or
          TypeKind.F64 or
          TypeKind.String;
    }

    private static bool IsInterpolatedNumeric(TypeSymbol type)
    {
      return type.TypeKind is TypeKind.I8 or
          TypeKind.U8 or
          TypeKind.I16 or
          TypeKind.U16 or
          TypeKind.I32 or
          TypeKind.U32 or
          TypeKind.I64 or
          TypeKind.U64 or
          TypeKind.F32 or
          TypeKind.F64;
    }

    private static bool IsNamed(TypeSymbol type, ISet<string> supportedTypes)
    {
      return type.TypeKind == TypeKind.Named &&
          (supportedTypes.Contains(type.QualifiedName) ||
           supportedTypes.Contains(type.RuntimeQualifiedName));
    }
  }

  internal sealed class StateVariableSymbol : VariableSymbol
  {
    public override SymbolKind Kind => SymbolKind.State;
    public bool IsPublic { get; }
    public StateSynchronizationMode? SynchronizationMode { get; }
    public bool IsSynchronized => SynchronizationMode.HasValue;
    public object InitialValue { get; }
    public TextSpan InitializerSpan { get; }
    public int Ordinal { get; }

    public StateVariableSymbol(
        string name,
        TypeSymbol type,
        bool isPublic,
        StateSynchronizationMode? synchronizationMode,
        object initialValue,
        TextSpan declarationSpan,
        TextSpan initializerSpan,
        int ordinal)
        : base(name, type, true, declarationSpan)
    {
      IsPublic = isPublic;
      SynchronizationMode = synchronizationMode;
      InitialValue = initialValue;
      InitializerSpan = initializerSpan;
      Ordinal = ordinal;
    }
  }

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

  internal sealed class LoopSymbol
  {
    public LoopSymbol(string label, bool isWhile, TextSpan sourceSpan)
    {
      Label = label;
      IsWhile = isWhile;
      SourceSpan = sourceSpan;
    }

    public string Label { get; }
    public bool IsWhile { get; }
    public TextSpan SourceSpan { get; }
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
    public ExternMemberKind MemberKind { get; }
    public MethodBase MethodBase { get; }
    public MethodInfo MethodInfo => MethodBase as MethodInfo;
    public override string ExternSignature { get; }

    public ExternMethodSymbol(
        string name,
        TypeSymbol containingType,
        IReadOnlyList<ParameterSymbol> parameters,
        TypeSymbol returnType,
        MethodBase methodInfo,
        string externSignature,
        bool? isStatic = null,
        ExternMemberKind memberKind = ExternMemberKind.Method)
        : base(
            name,
            containingType,
            parameters,
            returnType,
            isStatic ?? (methodInfo?.IsStatic ?? true))
    {
      MethodBase = methodInfo;
      ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature));
      MemberKind = memberKind;
    }
  }

  internal enum ExternMemberKind
  {
    Method,
    Getter,
    Setter,
    Constructor,
    Operator
  }

  internal sealed class UserMethodSymbol : MethodSymbol
  {
    public FunctionSymbol Function { get; }

    public UserMethodSymbol(FunctionSymbol function)
        : base(
            function?.Name ?? throw new ArgumentNullException(nameof(function)),
            function.ContainingType,
            function.Parameters,
            function.ReturnType,
            function.IsStatic)
    {
      Function = function;
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

    public SobakasuCompilationEnvironment(ExternCatalog externCatalog)
    {
      ExternCatalog = externCatalog ?? throw new ArgumentNullException(nameof(externCatalog));
      GlobalNamespace = externCatalog.GlobalNamespace;
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
    public IReadOnlyList<BoundConstantDeclaration> Constants { get; }
    public IReadOnlyList<BoundStateDeclaration> States { get; }
    public IReadOnlyList<BoundFunctionDeclaration> Functions { get; }
    public IReadOnlyList<BoundEventDeclaration> Events { get; }
    public IReadOnlyList<BoundNetworkReceiveDeclaration> NetworkReceivers { get; }

    public BoundProgram(
        IReadOnlyList<BoundConstantDeclaration> constants,
        IReadOnlyList<BoundStateDeclaration> states,
        IReadOnlyList<BoundFunctionDeclaration> functions,
        IReadOnlyList<BoundEventDeclaration> events,
        IReadOnlyList<BoundNetworkReceiveDeclaration> networkReceivers)
    {
      Constants = constants ?? throw new ArgumentNullException(nameof(constants));
      States = states ?? throw new ArgumentNullException(nameof(states));
      Functions = functions ?? throw new ArgumentNullException(nameof(functions));
      Events = events;
      NetworkReceivers = networkReceivers ??
          throw new ArgumentNullException(nameof(networkReceivers));
    }
  }

  internal sealed class BoundConstantDeclaration : BoundNode
  {
    public ConstantSymbol ConstantSymbol { get; }
    public BoundExpression Initializer { get; }

    public BoundConstantDeclaration(
        ConstantSymbol constantSymbol,
        BoundExpression initializer)
    {
      ConstantSymbol = constantSymbol ??
          throw new ArgumentNullException(nameof(constantSymbol));
      Initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
    }
  }

  internal sealed class BoundStateDeclaration : BoundNode
  {
    public StateVariableSymbol StateSymbol { get; }
    public BoundExpression Initializer { get; }

    public BoundStateDeclaration(
        StateVariableSymbol stateSymbol,
        BoundExpression initializer)
    {
      StateSymbol = stateSymbol ?? throw new ArgumentNullException(nameof(stateSymbol));
      Initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
    }
  }

  internal sealed class BoundFunctionDeclaration : BoundNode
  {
    public FunctionSymbol FunctionSymbol { get; }
    public string Name => FunctionSymbol.Name;
    public BoundBlockStatement Body { get; }

    public BoundFunctionDeclaration(
        FunctionSymbol functionSymbol,
        BoundBlockStatement body)
    {
      FunctionSymbol = functionSymbol ?? throw new ArgumentNullException(nameof(functionSymbol));
      Body = body ?? throw new ArgumentNullException(nameof(body));
    }
  }

  internal sealed class BoundEventDeclaration : BoundNode
  {
    public BoundEventSymbol EventSymbol { get; }
    public string Name => EventSymbol.SourceName;
    public string ExportName => EventSymbol.UdonName;
    public BoundBlockStatement Body { get; }

    public BoundEventDeclaration(
        BoundEventSymbol eventSymbol,
        BoundBlockStatement body)
    {
      EventSymbol = eventSymbol ?? throw new ArgumentNullException(nameof(eventSymbol));
      Body = body;
    }
  }

  internal sealed class BoundNetworkReceiveDeclaration : BoundNode
  {
    public NetworkReceiveSymbol ReceiveSymbol { get; }
    public BoundBlockStatement Body { get; }

    public BoundNetworkReceiveDeclaration(
        NetworkReceiveSymbol receiveSymbol,
        BoundBlockStatement body)
    {
      ReceiveSymbol = receiveSymbol ??
          throw new ArgumentNullException(nameof(receiveSymbol));
      Body = body ?? throw new ArgumentNullException(nameof(body));
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

  internal sealed class BoundNetworkSendStatement : BoundStatement
  {
    public NetworkReceiveSymbol Receiver { get; }
    public IReadOnlyList<BoundExpression> Arguments { get; }
    public BoundExpression Target { get; }
    public TypeSymbol CurrentBehaviourType { get; }
    public string ExternSignature { get; }

    public BoundNetworkSendStatement(
        NetworkReceiveSymbol receiver,
        IReadOnlyList<BoundExpression> arguments,
        BoundExpression target,
        TypeSymbol currentBehaviourType,
        string externSignature)
    {
      Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
      Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
      Target = target ?? throw new ArgumentNullException(nameof(target));
      CurrentBehaviourType = currentBehaviourType ??
          throw new ArgumentNullException(nameof(currentBehaviourType));
      ExternSignature = externSignature ??
          throw new ArgumentNullException(nameof(externSignature));
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

  internal sealed class BoundReturnStatement : BoundStatement
  {
    public BoundExpression Expression { get; }

    public BoundReturnStatement(BoundExpression expression)
    {
      Expression = expression;
    }
  }

  internal sealed class BoundBreakStatement : BoundStatement
  {
    public LoopSymbol Target { get; }
    public BoundExpression Expression { get; }

    public BoundBreakStatement(
        LoopSymbol target,
        BoundExpression expression)
    {
      Target = target ?? throw new ArgumentNullException(nameof(target));
      Expression = expression;
    }
  }

  internal sealed class BoundContinueStatement : BoundStatement
  {
    public LoopSymbol Target { get; }

    public BoundContinueStatement(LoopSymbol target)
    {
      Target = target ?? throw new ArgumentNullException(nameof(target));
    }
  }

  internal sealed class BoundRedoStatement : BoundStatement
  {
    public LoopSymbol Target { get; }

    public BoundRedoStatement(LoopSymbol target)
    {
      Target = target ?? throw new ArgumentNullException(nameof(target));
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

  internal enum BoundUnaryOperatorKind
  {
    Identity,
    Negation,
    LogicalNegation,
    OnesComplement
  }

  internal sealed class BoundUnaryOperator
  {
    public BoundUnaryOperatorKind Kind { get; }
    public Syntax.SyntaxKind SyntaxKind { get; }
    public TypeSymbol OperandType { get; }
    public TypeSymbol Type { get; }
    public string ExternSignature { get; }

    public BoundUnaryOperator(
        BoundUnaryOperatorKind kind,
        Syntax.SyntaxKind syntaxKind,
        TypeSymbol operandType,
        TypeSymbol type,
        string externSignature)
    {
      Kind = kind;
      SyntaxKind = syntaxKind;
      OperandType = operandType ?? throw new ArgumentNullException(nameof(operandType));
      Type = type ?? throw new ArgumentNullException(nameof(type));
      ExternSignature = externSignature ?? throw new ArgumentNullException(nameof(externSignature));
    }
  }

  internal sealed class BoundUnaryExpression : BoundExpression
  {
    public BoundUnaryOperator Operator { get; }
    public BoundExpression Operand { get; }
    public override TypeSymbol Type => Operator.Type;

    public BoundUnaryExpression(
        BoundUnaryOperator @operator,
        BoundExpression operand)
    {
      Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
      Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }
  }

  internal enum BoundBinaryOperatorKind
  {
    Addition,
    Subtraction,
    Multiplication,
    Division,
    Modulus,
    Equals,
    NotEquals,
    Less,
    LessOrEquals,
    Greater,
    GreaterOrEquals,
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    LeftShift,
    RightShift,
    LogicalAnd,
    LogicalOr
  }

  internal sealed class BoundBinaryOperator
  {
    public BoundBinaryOperatorKind Kind { get; }
    public Syntax.SyntaxKind SyntaxKind { get; }
    public TypeSymbol LeftType { get; }
    public TypeSymbol RightType { get; }
    public TypeSymbol Type { get; }
    public string ExternSignature { get; }
    public bool IsShortCircuit =>
        Kind == BoundBinaryOperatorKind.LogicalAnd ||
        Kind == BoundBinaryOperatorKind.LogicalOr;

    public BoundBinaryOperator(
        BoundBinaryOperatorKind kind,
        Syntax.SyntaxKind syntaxKind,
        TypeSymbol leftType,
        TypeSymbol rightType,
        TypeSymbol type,
        string externSignature = null)
    {
      Kind = kind;
      SyntaxKind = syntaxKind;
      LeftType = leftType ?? throw new ArgumentNullException(nameof(leftType));
      RightType = rightType ?? throw new ArgumentNullException(nameof(rightType));
      Type = type ?? throw new ArgumentNullException(nameof(type));
      ExternSignature = externSignature;
    }
  }

  internal sealed class BoundBinaryExpression : BoundExpression
  {
    public BoundExpression Left { get; }
    public BoundBinaryOperator Operator { get; }
    public BoundExpression Right { get; }
    public override TypeSymbol Type => Operator.Type;

    public BoundBinaryExpression(
        BoundExpression left,
        BoundBinaryOperator @operator,
        BoundExpression right)
    {
      Left = left ?? throw new ArgumentNullException(nameof(left));
      Operator = @operator ?? throw new ArgumentNullException(nameof(@operator));
      Right = right ?? throw new ArgumentNullException(nameof(right));
    }
  }

  internal sealed class BoundAssignmentExpression : BoundExpression
  {
    public VariableSymbol Variable { get; }
    public BoundExpression Expression { get; }
    public override TypeSymbol Type => Variable.Type;

    public BoundAssignmentExpression(
        VariableSymbol variable,
        BoundExpression expression)
    {
      Variable = variable ?? throw new ArgumentNullException(nameof(variable));
      Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }
  }

  internal sealed class BoundBlockExpression : BoundExpression
  {
    public BoundBlockStatement Block { get; }
    public BoundExpression TrailingExpression { get; }
    public override TypeSymbol Type { get; }

    public BoundBlockExpression(
        BoundBlockStatement block,
        BoundExpression trailingExpression,
        TypeSymbol type)
    {
      Block = block ?? throw new ArgumentNullException(nameof(block));
      TrailingExpression = trailingExpression;
      Type = type ?? throw new ArgumentNullException(nameof(type));
    }
  }

  internal sealed class BoundIfExpression : BoundExpression
  {
    public BoundExpression Condition { get; }
    public BoundBlockExpression ThenExpression { get; }
    public BoundExpression ElseExpression { get; }
    public override TypeSymbol Type { get; }

    public BoundIfExpression(
        BoundExpression condition,
        BoundBlockExpression thenExpression,
        BoundExpression elseExpression,
        TypeSymbol type)
    {
      Condition = condition ?? throw new ArgumentNullException(nameof(condition));
      ThenExpression = thenExpression ?? throw new ArgumentNullException(nameof(thenExpression));
      ElseExpression = elseExpression;
      Type = type ?? throw new ArgumentNullException(nameof(type));
    }
  }

  internal abstract class BoundPattern : BoundNode
  {
    protected BoundPattern(TextSpan span)
    {
      Span = span;
    }

    public TextSpan Span { get; }
  }

  internal sealed class BoundInvalidPattern : BoundPattern
  {
    public BoundInvalidPattern(TextSpan span)
        : base(span)
    {
    }
  }

  internal sealed class BoundWildcardPattern : BoundPattern
  {
    public BoundWildcardPattern(TextSpan span)
        : base(span)
    {
    }
  }

  internal sealed class BoundLiteralPattern : BoundPattern
  {
    public BoundLiteralExpression Literal { get; }
    public BoundBinaryOperator ComparisonOperator { get; }

    public BoundLiteralPattern(
        BoundLiteralExpression literal,
        BoundBinaryOperator comparisonOperator,
        TextSpan span)
        : base(span)
    {
      Literal = literal ?? throw new ArgumentNullException(nameof(literal));
      ComparisonOperator = comparisonOperator;
    }
  }

  internal sealed class BoundPatternBinding
  {
    public AggregateFieldSymbol Field { get; }
    public LocalVariableSymbol Variable { get; }

    public BoundPatternBinding(
        AggregateFieldSymbol field,
        LocalVariableSymbol variable)
    {
      Field = field ?? throw new ArgumentNullException(nameof(field));
      Variable = variable ?? throw new ArgumentNullException(nameof(variable));
    }
  }

  internal sealed class BoundEnumVariantPattern : BoundPattern
  {
    public EnumVariantSymbol Variant { get; }
    public IReadOnlyList<BoundPatternBinding> Bindings { get; }
    public BoundBinaryOperator TagComparisonOperator { get; }

    public BoundEnumVariantPattern(
        EnumVariantSymbol variant,
        IReadOnlyList<BoundPatternBinding> bindings,
        BoundBinaryOperator tagComparisonOperator,
        TextSpan span)
        : base(span)
    {
      Variant = variant ?? throw new ArgumentNullException(nameof(variant));
      Bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
      TagComparisonOperator = tagComparisonOperator;
    }
  }

  internal sealed class BoundMatchArm
  {
    public BoundPattern Pattern { get; }
    public BoundExpression Expression { get; }
    public bool IsReachable { get; }

    public BoundMatchArm(
        BoundPattern pattern,
        BoundExpression expression,
        bool isReachable)
    {
      Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
      Expression = expression ?? throw new ArgumentNullException(nameof(expression));
      IsReachable = isReachable;
    }
  }

  internal sealed class BoundMatchExpression : BoundExpression
  {
    public BoundExpression Expression { get; }
    public IReadOnlyList<BoundMatchArm> Arms { get; }
    public override TypeSymbol Type { get; }

    public BoundMatchExpression(
        BoundExpression expression,
        IReadOnlyList<BoundMatchArm> arms,
        TypeSymbol type)
    {
      Expression = expression ?? throw new ArgumentNullException(nameof(expression));
      Arms = arms ?? throw new ArgumentNullException(nameof(arms));
      Type = type ?? throw new ArgumentNullException(nameof(type));
    }
  }

  internal sealed class BoundWhileExpression : BoundExpression
  {
    public LoopSymbol Loop { get; }
    public BoundExpression Condition { get; }
    public BoundBlockExpression Body { get; }
    public override TypeSymbol Type => TypeSymbol.U0;

    public BoundWhileExpression(
        LoopSymbol loop,
        BoundExpression condition,
        BoundBlockExpression body)
    {
      Loop = loop ?? throw new ArgumentNullException(nameof(loop));
      Condition = condition ?? throw new ArgumentNullException(nameof(condition));
      Body = body ?? throw new ArgumentNullException(nameof(body));
    }
  }

  internal sealed class BoundLoopExpression : BoundExpression
  {
    public LoopSymbol Loop { get; }
    public BoundBlockExpression Body { get; }
    public override TypeSymbol Type { get; }

    public BoundLoopExpression(
        LoopSymbol loop,
        BoundBlockExpression body,
        TypeSymbol type)
    {
      Loop = loop ?? throw new ArgumentNullException(nameof(loop));
      Body = body ?? throw new ArgumentNullException(nameof(body));
      Type = type ?? throw new ArgumentNullException(nameof(type));
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

  internal sealed class BoundAggregateFieldInitializer
  {
    public AggregateFieldSymbol Field { get; }
    public BoundExpression Expression { get; }

    public BoundAggregateFieldInitializer(
        AggregateFieldSymbol field,
        BoundExpression expression)
    {
      Field = field ?? throw new ArgumentNullException(nameof(field));
      Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }
  }

  internal sealed class BoundStructConstructionExpression : BoundExpression
  {
    public IReadOnlyList<BoundAggregateFieldInitializer> Initializers { get; }
    public override TypeSymbol Type { get; }

    public BoundStructConstructionExpression(
        TypeSymbol type,
        IReadOnlyList<BoundAggregateFieldInitializer> initializers)
    {
      Type = type ?? throw new ArgumentNullException(nameof(type));
      Initializers = initializers ?? throw new ArgumentNullException(nameof(initializers));
    }
  }

  internal sealed class BoundEnumConstructionExpression : BoundExpression
  {
    public EnumVariantSymbol Variant { get; }
    public IReadOnlyList<BoundAggregateFieldInitializer> Initializers { get; }
    public override TypeSymbol Type => Variant.ContainingType;

    public BoundEnumConstructionExpression(
        EnumVariantSymbol variant,
        IReadOnlyList<BoundAggregateFieldInitializer> initializers)
    {
      Variant = variant ?? throw new ArgumentNullException(nameof(variant));
      Initializers = initializers ?? throw new ArgumentNullException(nameof(initializers));
    }
  }

  internal sealed class BoundAggregateFieldAccessExpression : BoundExpression
  {
    public BoundExpression Receiver { get; }
    public AggregateFieldSymbol Field { get; }
    public override TypeSymbol Type => Field.Type;

    public BoundAggregateFieldAccessExpression(
        BoundExpression receiver,
        AggregateFieldSymbol field)
    {
      Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
      Field = field ?? throw new ArgumentNullException(nameof(field));
    }
  }

  internal sealed class BoundAggregateFieldAssignmentExpression : BoundExpression
  {
    public BoundAggregateFieldAccessExpression Target { get; }
    public BoundExpression Value { get; }
    public BoundBinaryOperator CompoundOperator { get; }
    public override TypeSymbol Type => Target.Type;

    public BoundAggregateFieldAssignmentExpression(
        BoundAggregateFieldAccessExpression target,
        BoundExpression value,
        BoundBinaryOperator compoundOperator = null)
    {
      Target = target ?? throw new ArgumentNullException(nameof(target));
      Value = value ?? throw new ArgumentNullException(nameof(value));
      CompoundOperator = compoundOperator;
    }
  }

  internal sealed class BoundArrayLiteralExpression : BoundExpression
  {
    public IReadOnlyList<BoundExpression> Elements { get; }
    public TypeSymbol ElementType { get; }
    public ArrayIntrinsicSymbols Intrinsics { get; }
    public IReadOnlyList<ArrayIntrinsicSymbols> AggregateLeafIntrinsics { get; }
    public override TypeSymbol Type { get; }

    public BoundArrayLiteralExpression(
        IReadOnlyList<BoundExpression> elements,
        TypeSymbol arrayType,
        ArrayIntrinsicSymbols intrinsics,
        IReadOnlyList<ArrayIntrinsicSymbols> aggregateLeafIntrinsics = null)
    {
      Elements = elements;
      Type = arrayType ?? throw new ArgumentNullException(nameof(arrayType));
      ElementType = arrayType.ElementType;
      Intrinsics = intrinsics;
      AggregateLeafIntrinsics = aggregateLeafIntrinsics;
    }
  }

  internal sealed class ArrayIntrinsicSymbols
  {
    public string ConstructorExternSignature { get; }
    public string GetterExternSignature { get; }
    public string SetterExternSignature { get; }
    public string LengthExternSignature { get; }
    public TypeSymbol IndexType { get; }

    public ArrayIntrinsicSymbols(
        string constructorExternSignature,
        string getterExternSignature,
        string setterExternSignature,
        string lengthExternSignature,
        TypeSymbol indexType)
    {
      ConstructorExternSignature = constructorExternSignature ??
          throw new ArgumentNullException(nameof(constructorExternSignature));
      GetterExternSignature = getterExternSignature ??
          throw new ArgumentNullException(nameof(getterExternSignature));
      SetterExternSignature = setterExternSignature ??
          throw new ArgumentNullException(nameof(setterExternSignature));
      LengthExternSignature = lengthExternSignature ??
          throw new ArgumentNullException(nameof(lengthExternSignature));
      IndexType = indexType ?? throw new ArgumentNullException(nameof(indexType));
    }
  }

  internal sealed class BoundArrayRepeatExpression : BoundExpression
  {
    public BoundExpression Operand { get; }
    public BoundExpression Length { get; }
    public bool UsesDefaultValue => Operand == null;
    public ArrayIntrinsicSymbols Intrinsics { get; }
    public BoundBinaryOperator IndexLessThanOperator { get; }
    public BoundBinaryOperator IndexIncrementOperator { get; }
    public IReadOnlyList<ArrayIntrinsicSymbols> AggregateLeafIntrinsics { get; }
    public override TypeSymbol Type { get; }

    public BoundArrayRepeatExpression(
        TypeSymbol arrayType,
        BoundExpression operand,
        BoundExpression length,
        ArrayIntrinsicSymbols intrinsics,
        BoundBinaryOperator indexLessThanOperator,
        BoundBinaryOperator indexIncrementOperator,
        IReadOnlyList<ArrayIntrinsicSymbols> aggregateLeafIntrinsics = null)
    {
      Type = arrayType ?? throw new ArgumentNullException(nameof(arrayType));
      Operand = operand;
      Length = length ?? throw new ArgumentNullException(nameof(length));
      Intrinsics = intrinsics;
      IndexLessThanOperator = indexLessThanOperator;
      IndexIncrementOperator = indexIncrementOperator;
      AggregateLeafIntrinsics = aggregateLeafIntrinsics;
    }
  }

  internal sealed class BoundElementAccessExpression : BoundExpression
  {
    public BoundExpression Array { get; }
    public BoundExpression Index { get; }
    public ArrayIntrinsicSymbols Intrinsics { get; }
    public IReadOnlyList<ArrayIntrinsicSymbols> AggregateLeafIntrinsics { get; }
    public override TypeSymbol Type { get; }

    public BoundElementAccessExpression(
        BoundExpression array,
        BoundExpression index,
        ArrayIntrinsicSymbols intrinsics,
        IReadOnlyList<ArrayIntrinsicSymbols> aggregateLeafIntrinsics = null)
    {
      Array = array ?? throw new ArgumentNullException(nameof(array));
      Index = index ?? throw new ArgumentNullException(nameof(index));
      Intrinsics = intrinsics;
      AggregateLeafIntrinsics = aggregateLeafIntrinsics;
      Type = array.Type.ElementType;
    }
  }

  internal sealed class BoundElementAssignmentExpression : BoundExpression
  {
    public BoundElementAccessExpression Target { get; }
    public BoundExpression Value { get; }
    public BoundBinaryOperator CompoundOperator { get; }
    public override TypeSymbol Type => Target.Type;

    public BoundElementAssignmentExpression(
        BoundElementAccessExpression target,
        BoundExpression value,
        BoundBinaryOperator compoundOperator = null)
    {
      Target = target ?? throw new ArgumentNullException(nameof(target));
      Value = value ?? throw new ArgumentNullException(nameof(value));
      CompoundOperator = compoundOperator;
    }
  }

  internal sealed class BoundArrayLengthExpression : BoundExpression
  {
    public BoundExpression Array { get; }
    public ArrayIntrinsicSymbols Intrinsics { get; }
    public IReadOnlyList<ArrayIntrinsicSymbols> AggregateLeafIntrinsics { get; }
    public override TypeSymbol Type => TypeSymbol.I32;

    public BoundArrayLengthExpression(
        BoundExpression array,
        ArrayIntrinsicSymbols intrinsics,
        IReadOnlyList<ArrayIntrinsicSymbols> aggregateLeafIntrinsics = null)
    {
      Array = array ?? throw new ArgumentNullException(nameof(array));
      Intrinsics = intrinsics;
      AggregateLeafIntrinsics = aggregateLeafIntrinsics;
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

  internal sealed class BoundUserFunctionCallExpression : BoundExpression
  {
    public FunctionSymbol Function { get; }
    public BoundExpression Receiver { get; }
    public IReadOnlyList<BoundExpression> Arguments { get; }
    public override TypeSymbol Type => Function.ReturnType;

    public BoundUserFunctionCallExpression(
        FunctionSymbol function,
        IReadOnlyList<BoundExpression> arguments,
        BoundExpression receiver = null)
    {
      Function = function ?? throw new ArgumentNullException(nameof(function));
      Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
      Receiver = receiver;
    }
  }
}
