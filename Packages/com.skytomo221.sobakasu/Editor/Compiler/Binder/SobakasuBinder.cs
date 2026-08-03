using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class SobakasuBinder
  {
    private static readonly IReadOnlyDictionary<string, TypeSymbol> BuiltInTypes =
        new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
        {
          ["u0"] = TypeSymbol.U0,
          ["i8"] = TypeSymbol.I8,
          ["u8"] = TypeSymbol.U8,
          ["i16"] = TypeSymbol.I16,
          ["u16"] = TypeSymbol.U16,
          ["i32"] = TypeSymbol.I32,
          ["u32"] = TypeSymbol.U32,
          ["i64"] = TypeSymbol.I64,
          ["u64"] = TypeSymbol.U64,
          ["f32"] = TypeSymbol.F32,
          ["f64"] = TypeSymbol.F64,
          ["char"] = TypeSymbol.Char,
          ["string"] = TypeSymbol.String,
          ["bool"] = TypeSymbol.Bool
        };

    private readonly SobakasuCompilationEnvironment _environment;
    private BoundScope _scope;
    private readonly Dictionary<string, FunctionSymbol> _functionSymbols =
        new(StringComparer.Ordinal);
    private readonly Dictionary<FunctionDeclarationSyntax, FunctionSymbol> _functionSymbolsBySyntax =
        new();
    private readonly Dictionary<string, StateVariableSymbol> _stateSymbols =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeSymbol> _declaredTypes =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeSymbol> _externalBindingsByRuntimeType =
        new(StringComparer.Ordinal);
    private readonly Dictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>> _methodGroupsByType =
        new();
    private readonly Dictionary<FunctionDeclarationSyntax, FunctionSymbol> _methodSymbolsBySyntax =
        new();
    private TypeSymbol _currentType;
    private FunctionSymbol _currentFunction;
    private StandardLibraryModule _currentModule;
    private readonly Dictionary<StandardLibraryModule, Dictionary<string, FunctionSymbol>> _moduleFunctions =
        new();
    private readonly Dictionary<StandardLibraryModule, Dictionary<string, TypeSymbol>> _moduleTypes =
        new();
    private readonly Dictionary<StandardLibraryModule, Dictionary<string, Symbol>> _moduleImports =
        new();
    private readonly Dictionary<FunctionDeclarationSyntax, StandardLibraryModule> _functionModulesBySyntax =
        new();
    private readonly Dictionary<FunctionSymbol, StandardLibraryModule> _modulesByFunctionSymbol =
        new();
    private readonly List<LoopBindingContext> _loopContexts = new();
    private TypeSymbol _currentReturnType = TypeSymbol.U0;
    private string _currentEventName = string.Empty;
    private bool _sawValueReturn;

    public DiagnosticBag Diagnostics { get; } = new();

    public SobakasuBinder()
        : this(SobakasuBuiltInEnvironment.Default)
    {
    }

    internal SobakasuBinder(SobakasuCompilationEnvironment environment)
    {
      _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public BoundProgram BindProgram(CompilationUnitSyntax syntax)
    {
      return BindProgram(StandardLibraryModuleGraph.CreateSingle(syntax));
    }

    internal BoundProgram BindProgram(StandardLibraryModuleGraph graph)
    {
      _functionSymbols.Clear();
      _functionSymbolsBySyntax.Clear();
      _stateSymbols.Clear();
      _declaredTypes.Clear();
      _externalBindingsByRuntimeType.Clear();
      _methodGroupsByType.Clear();
      _methodSymbolsBySyntax.Clear();
      _moduleFunctions.Clear();
      _moduleTypes.Clear();
      _moduleImports.Clear();
      _functionModulesBySyntax.Clear();
      _modulesByFunctionSymbol.Clear();
      _loopContexts.Clear();

      foreach (var module in graph.Modules)
      {
        _moduleTypes[module] = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal);
        _moduleFunctions[module] = new Dictionary<string, FunctionSymbol>(StringComparer.Ordinal);
        _moduleImports[module] = new Dictionary<string, Symbol>(StringComparer.Ordinal);
      }

      foreach (var module in graph.Modules)
      {
        SetCurrentModule(module, includeFunctions: false);
        foreach (var member in module.Syntax.Members)
        {
          if (member is ImplDeclarationSyntax implDeclaration &&
              implDeclaration.IsExternalBinding)
          {
            CollectExternalTypeBinding(implDeclaration);
          }
        }

        _moduleTypes[module] = new Dictionary<string, TypeSymbol>(
            _declaredTypes,
            StringComparer.Ordinal);
      }

      BuildModuleImports(graph, includeFunctions: false);

      foreach (var module in graph.Modules)
      {
        SetCurrentModule(module, includeFunctions: false);
        foreach (var member in module.Syntax.Members)
        {
          if (member is FunctionDeclarationSyntax functionDeclaration)
          {
            CollectFunctionSignature(functionDeclaration);
            _functionModulesBySyntax[functionDeclaration] = module;
            if (_functionSymbolsBySyntax.TryGetValue(
                    functionDeclaration,
                    out var collectedFunction))
            {
              _modulesByFunctionSymbol[collectedFunction] = module;
            }
          }
        }

        _moduleFunctions[module] = new Dictionary<string, FunctionSymbol>(
            _functionSymbols,
            StringComparer.Ordinal);
      }

      BuildModuleImports(graph, includeFunctions: true);

      foreach (var module in graph.Modules)
      {
        SetCurrentModule(module, includeFunctions: true);
        foreach (var member in module.Syntax.Members)
        {
          if (member is ImplDeclarationSyntax implDeclaration)
          {
            CollectImplMethodSignatures(implDeclaration);
            foreach (var method in implDeclaration.Methods)
            {
              _functionModulesBySyntax[method] = module;
              if (_methodSymbolsBySyntax.TryGetValue(method, out var collectedMethod))
                _modulesByFunctionSymbol[collectedMethod] = module;
            }
          }
        }
      }

      SetCurrentModule(graph.EntryModule, includeFunctions: true);
      var states = BindStateDeclarations(graph.EntryModule.Syntax.Members);

      var functions = new List<BoundFunctionDeclaration>();
      foreach (var module in graph.Modules)
      {
        SetCurrentModule(module, includeFunctions: true);
        foreach (var member in module.Syntax.Members)
        {
          if (member is FunctionDeclarationSyntax functionDeclaration &&
              _functionSymbolsBySyntax.TryGetValue(functionDeclaration, out var functionSymbol))
          {
            functions.Add(BindFunctionDeclaration(functionDeclaration, functionSymbol));
          }

          if (member is not ImplDeclarationSyntax implDeclaration)
            continue;

          foreach (var methodSyntax in implDeclaration.Methods)
          {
            if (_methodSymbolsBySyntax.TryGetValue(methodSyntax, out var methodSymbol))
              functions.Add(BindFunctionDeclaration(methodSyntax, methodSymbol));
          }
        }
      }

      ReportRecursiveFunctions(functions);

      var events = new List<BoundEventDeclaration>();
      var declaredEvents = new HashSet<string>(StringComparer.Ordinal);

      foreach (var module in graph.Modules)
      {
        SetCurrentModule(module, includeFunctions: true);
        foreach (var member in module.Syntax.Members)
        {
          if (member is UseDirectiveSyntax ||
              member is FunctionDeclarationSyntax ||
              member is ImplDeclarationSyntax)
            continue;

          if (member is StateDeclarationSyntax)
          {
            if (module.IsStandardLibrary)
            {
              Diagnostics.ReportStateNotAllowedInStandardLibrary(
                  GetMemberSpan(member));
            }
            continue;
          }

          if (member is EventDeclarationSyntax eventDeclaration)
          {
            if (module.IsStandardLibrary)
            {
              Diagnostics.ReportEventNotAllowedInStandardLibrary(
                  eventDeclaration.OnKeyword.Span);
            }
            else
            {
              events.Add(BindEventDeclaration(eventDeclaration, declaredEvents));
            }
            continue;
          }

          if (member is SkippedMemberSyntax skippedMember)
          {
            Diagnostics.ReportUnsupportedMember(
                skippedMember.BadToken.Span,
                skippedMember.BadToken.Text ?? "");
            continue;
          }

          Diagnostics.ReportUnsupportedMember(
              module.Syntax.EndOfFileToken.Span,
              member.GetType().Name);
        }
      }

      return new BoundProgram(states, functions, events);
    }

    private void SetCurrentModule(
        StandardLibraryModule module,
        bool includeFunctions)
    {
      _currentModule = module;
      Diagnostics.SourcePath = module?.SourcePath ?? string.Empty;
      _functionSymbols.Clear();
      _declaredTypes.Clear();

      if (module == null)
        return;

      if (_moduleTypes.TryGetValue(module, out var types))
      {
        foreach (var pair in types)
          _declaredTypes[pair.Key] = pair.Value;
      }

      if (includeFunctions && _moduleFunctions.TryGetValue(module, out var functions))
      {
        foreach (var pair in functions)
          _functionSymbols[pair.Key] = pair.Value;
      }

      if (!_moduleImports.TryGetValue(module, out var imports))
        return;

      foreach (var pair in imports)
      {
        if (pair.Value is TypeSymbol importedType)
        {
          if (!_declaredTypes.ContainsKey(pair.Key))
            _declaredTypes.Add(pair.Key, importedType);
        }
        else if (includeFunctions && pair.Value is FunctionSymbol importedFunction)
        {
          if (!_functionSymbols.ContainsKey(pair.Key))
            _functionSymbols.Add(pair.Key, importedFunction);
        }
      }
    }

    private void BuildModuleImports(
        StandardLibraryModuleGraph graph,
        bool includeFunctions)
    {
      foreach (var module in graph.Modules)
      {
        var imports = _moduleImports[module];
        imports.Clear();
        var resolvedSyntax = new HashSet<UseDirectiveSyntax>();

        foreach (var import in module.Imports)
        {
          resolvedSyntax.Add(import.Syntax);
          Symbol symbol = null;
          if (_moduleTypes.TryGetValue(import.TargetModule, out var targetTypes) &&
              targetTypes.TryGetValue(import.DeclarationName, out var importedType))
          {
            symbol = importedType;
            if (!importedType.IsPublic)
            {
              if (includeFunctions)
              {
                Diagnostics.SourcePath = module.SourcePath;
                Diagnostics.ReportDeclarationNotPublic(
                    GetUseDirectiveSpan(import.Syntax),
                    import.DeclarationName);
              }
              continue;
            }
          }
          else if (includeFunctions &&
                   _moduleFunctions.TryGetValue(import.TargetModule, out var targetFunctions) &&
                   targetFunctions.TryGetValue(import.DeclarationName, out var importedFunction))
          {
            symbol = importedFunction;
            if (!importedFunction.IsPublic)
            {
              Diagnostics.SourcePath = module.SourcePath;
              Diagnostics.ReportDeclarationNotPublic(
                  GetUseDirectiveSpan(import.Syntax),
                  import.DeclarationName);
              continue;
            }
          }

          if (symbol == null)
          {
            if (includeFunctions)
            {
              Diagnostics.SourcePath = module.SourcePath;
              Diagnostics.ReportLogicalDeclarationNotFound(
                  GetUseDirectiveSpan(import.Syntax),
                  import.Syntax.Path.GetText());
            }
            continue;
          }

          if (imports.TryGetValue(import.IntroducedName, out var existing))
          {
            Diagnostics.SourcePath = module.SourcePath;
            if (import.Syntax.Alias != null)
            {
              Diagnostics.ReportDuplicateModuleAlias(
                  import.Syntax.Alias.Span,
                  import.IntroducedName);
            }
            else
            {
              Diagnostics.ReportAmbiguousModuleImport(
                  GetUseDirectiveSpan(import.Syntax),
                  import.IntroducedName,
                  GetSymbolDisplayName(existing),
                  GetSymbolDisplayName(symbol));
            }
            continue;
          }

          imports.Add(import.IntroducedName, symbol);
        }

        if (!includeFunctions)
          continue;

        foreach (var member in module.Syntax.Members)
        {
          if (member is not UseDirectiveSyntax use ||
              use.IsMalformed ||
              resolvedSyntax.Contains(use))
          {
            continue;
          }

          Diagnostics.SourcePath = module.SourcePath;
          var path = use.Path.GetText();
          if (LooksLikeExternalUse(path))
          {
            Diagnostics.ReportExternalApiCannotBeImportedWithUse(
                GetUseDirectiveSpan(use),
                path);
          }
          else
          {
            Diagnostics.ReportLogicalModuleDoesNotExist(
                GetUseDirectiveSpan(use),
                path);
          }
        }
      }
    }

    private static bool LooksLikeExternalUse(string path)
    {
      return path == "System" || path.StartsWith("System.", StringComparison.Ordinal) ||
             path == "UnityEngine" || path.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
             path == "VRC" || path.StartsWith("VRC.", StringComparison.Ordinal) ||
             path == "TMPro" || path.StartsWith("TMPro.", StringComparison.Ordinal);
    }

    private void CollectExternalTypeBinding(ImplDeclarationSyntax syntax)
    {
      var typeName = syntax.TargetType.GetText();
      var span = syntax.TargetType.GetSpan();
      if (syntax.TargetType.Parts.Count != 1 ||
          syntax.TargetType.Parts[0].Kind != SyntaxKind.Identifier)
      {
        Diagnostics.ReportInvalidExternalBindingTarget(span, typeName);
        return;
      }

      if (BuiltInTypes.ContainsKey(typeName))
      {
        Diagnostics.ReportCannotExternallyBindBuiltInType(span, typeName);
        return;
      }

      if (_declaredTypes.ContainsKey(typeName))
      {
        Diagnostics.ReportDuplicateExternalTypeBinding(span, typeName);
        return;
      }

      var runtimeTypeName = syntax.ExternalTypeName?.GetText() ?? string.Empty;
      if (!_environment.ExternCatalog.TryGetTypeSymbol(runtimeTypeName, out var runtimeType))
      {
        Diagnostics.ReportUnknownExternalType(span, runtimeTypeName);
        return;
      }

      if (runtimeType.IsBuiltIn)
      {
        Diagnostics.ReportCannotExternallyBindBuiltInType(span, runtimeTypeName);
        return;
      }

      if (!_environment.ExternCatalog.IsTypeExposed(runtimeType))
      {
        Diagnostics.ReportExternalTypeNotExposed(span, runtimeTypeName);
        return;
      }

      if (_externalBindingsByRuntimeType.TryGetValue(
              runtimeType.RuntimeQualifiedName,
              out var existingBinding))
      {
        Diagnostics.ReportExternalRuntimeTypeAlreadyBound(
            span,
            runtimeTypeName,
            existingBinding.Name);
        return;
      }

      var type = TypeSymbol.CreateExternalBinding(
          typeName,
          string.IsNullOrEmpty(_currentModule?.LogicalName)
              ? typeName
              : $"{_currentModule.LogicalName}.{typeName}",
          runtimeType,
          syntax.PubKeyword != null,
          _currentModule?.LogicalName);
      _declaredTypes.Add(typeName, type);
      _externalBindingsByRuntimeType.Add(type.RuntimeQualifiedName, type);
    }

    private void CollectImplMethodSignatures(ImplDeclarationSyntax syntax)
    {
      var targetName = syntax.TargetType.GetText();
      TypeSymbol targetType;
      if (syntax.IsExternalBinding)
      {
        if (!_declaredTypes.TryGetValue(targetName, out targetType))
          return;
      }
      else
      {
        if (syntax.PubKeyword != null)
        {
          Diagnostics.ReportPublicModifierNotAllowedOnAdditionalImpl(
              syntax.PubKeyword.Span);
        }

        targetType = BindTypeSyntax(syntax.TargetType);
        if (targetType == TypeSymbol.Error)
        {
          Diagnostics.ReportUnknownImplTarget(
              syntax.TargetType.GetSpan(),
              targetName);
          return;
        }
      }

      var previousType = _currentType;
      _currentType = targetType;
      try
      {
        foreach (var methodSyntax in syntax.Methods)
          CollectImplMethodSignature(methodSyntax, targetType);
      }
      finally
      {
        _currentType = previousType;
      }
    }

    private void CollectImplMethodSignature(
        FunctionDeclarationSyntax syntax,
        TypeSymbol targetType)
    {
      var isStatic = syntax.StaticKeyword != null;
      var isOperator = syntax.OperatorToken != null;
      var operatorKind = syntax.OperatorToken?.Kind;
      var parameters = BindMethodParameters(syntax.Parameters);
      var returnType = syntax.ReturnTypeAnnotation == null
          ? TypeSymbol.U0
          : BindTypeSyntax(syntax.ReturnTypeAnnotation.Type);
      var nameSpan = GetFunctionNameSpan(syntax);

      if (isOperator)
      {
        ValidateOperatorDeclaration(
            syntax,
            targetType,
            parameters,
            returnType,
            nameSpan);
      }

      var selfParameter = isStatic
          ? null
          : new ParameterSymbol("self", targetType, -1, "self", nameSpan);
      var symbol = new FunctionSymbol(
          syntax.Name,
          returnType,
          parameters,
          nameSpan,
          targetType,
          selfParameter,
          isStatic,
          syntax.PubKeyword != null,
          isOperator,
          operatorKind,
          _currentModule?.LogicalName);
      _methodSymbolsBySyntax[syntax] = symbol;

      var methodGroup = GetOrCreateUserMethodGroup(targetType, symbol.Name);
      foreach (var existing in methodGroup.Methods)
      {
        if (HaveSameParameterTypes(existing.Parameters, symbol.Parameters))
        {
          Diagnostics.ReportDuplicateMethodSignature(
              nameSpan,
              symbol.DisplayName);
          return;
        }
      }

      methodGroup.AddMethod(new UserMethodSymbol(symbol));
    }

    private IReadOnlyList<ParameterSymbol> BindMethodParameters(
        IReadOnlyList<ParameterSyntax> parameterSyntaxes)
    {
      var parameters = new List<ParameterSymbol>();
      var names = new HashSet<string>(StringComparer.Ordinal);
      foreach (var syntax in parameterSyntaxes)
      {
        var name = syntax.Identifier.Text ?? string.Empty;
        if (string.Equals(name, "self", StringComparison.Ordinal))
        {
          Diagnostics.ReportExplicitSelfParameter(syntax.Identifier.Span);
          continue;
        }

        if (!names.Add(name))
          Diagnostics.ReportDuplicateParameterName(syntax.Identifier.Span, name);

        parameters.Add(new ParameterSymbol(
            name,
            BindTypeSyntax(syntax.Type),
            parameters.Count,
            name,
            syntax.Identifier.Span));
      }

      return parameters;
    }

    private void ValidateOperatorDeclaration(
        FunctionDeclarationSyntax syntax,
        TypeSymbol targetType,
        IReadOnlyList<ParameterSymbol> parameters,
        TypeSymbol returnType,
        TextSpan span)
    {
      var kind = syntax.OperatorToken.Kind;
      var isUnary = syntax.AtToken != null;
      if (syntax.StaticKeyword != null)
        Diagnostics.ReportInvalidOperatorName(span, syntax.Name);

      if (isUnary)
      {
        if (kind != SyntaxKind.PlusToken &&
            kind != SyntaxKind.MinusToken &&
            kind != SyntaxKind.BangToken &&
            kind != SyntaxKind.TildeToken)
        {
          Diagnostics.ReportInvalidOperatorName(span, syntax.Name);
        }

        if (parameters.Count != 0)
          Diagnostics.ReportInvalidUnaryOperatorArity(span, syntax.Name);
      }
      else
      {
        if (!IsOverloadableBinaryOperator(kind))
          Diagnostics.ReportOperatorCannotBeOverloaded(span, GetOperatorText(kind));

        if (parameters.Count != 1)
          Diagnostics.ReportInvalidBinaryOperatorArity(span, syntax.Name);
      }

      if (IsComparisonOperator(kind) && returnType != TypeSymbol.Bool)
        Diagnostics.ReportComparisonOperatorMustReturnBool(span, syntax.Name);

      if (targetType.IsBuiltIn &&
          IsBuiltInOperatorSignature(targetType, kind, isUnary, parameters))
      {
        Diagnostics.ReportBuiltInOperatorCannotBeRedefined(span, syntax.Name);
      }
    }

    private static bool IsOverloadableBinaryOperator(SyntaxKind kind)
    {
      switch (kind)
      {
        case SyntaxKind.PlusToken:
        case SyntaxKind.MinusToken:
        case SyntaxKind.StarToken:
        case SyntaxKind.SlashToken:
        case SyntaxKind.PercentToken:
        case SyntaxKind.EqualsEqualsToken:
        case SyntaxKind.BangEqualsToken:
        case SyntaxKind.LessToken:
        case SyntaxKind.LessOrEqualsToken:
        case SyntaxKind.GreaterToken:
        case SyntaxKind.GreaterOrEqualsToken:
        case SyntaxKind.AmpersandToken:
        case SyntaxKind.PipeToken:
        case SyntaxKind.CaretToken:
        case SyntaxKind.LessLessToken:
        case SyntaxKind.GreaterGreaterToken:
          return true;

        default:
          return false;
      }
    }

    private static bool IsComparisonOperator(SyntaxKind kind)
    {
      return kind == SyntaxKind.EqualsEqualsToken ||
             kind == SyntaxKind.BangEqualsToken ||
             kind == SyntaxKind.LessToken ||
             kind == SyntaxKind.LessOrEqualsToken ||
             kind == SyntaxKind.GreaterToken ||
             kind == SyntaxKind.GreaterOrEqualsToken;
    }

    private static bool IsBuiltInOperatorSignature(
        TypeSymbol targetType,
        SyntaxKind kind,
        bool isUnary,
        IReadOnlyList<ParameterSymbol> parameters)
    {
      if (isUnary)
      {
        return (kind == SyntaxKind.PlusToken || kind == SyntaxKind.MinusToken) &&
                   IsNumericType(targetType) ||
               kind == SyntaxKind.BangToken && targetType == TypeSymbol.Bool ||
               kind == SyntaxKind.TildeToken && IsIntegerType(targetType);
      }

      if (parameters.Count != 1)
        return false;

      var rightType = parameters[0].Type;
      if (kind == SyntaxKind.LessLessToken || kind == SyntaxKind.GreaterGreaterToken)
        return IsIntegerType(targetType) && rightType == TypeSymbol.I32;

      if (kind == SyntaxKind.AmpersandToken ||
          kind == SyntaxKind.PipeToken ||
          kind == SyntaxKind.CaretToken)
      {
        return targetType == rightType &&
               (IsIntegerType(targetType) || targetType == TypeSymbol.Bool);
      }

      if (kind == SyntaxKind.EqualsEqualsToken ||
          kind == SyntaxKind.BangEqualsToken)
      {
        return targetType == rightType && IsEqualityPrimitiveType(targetType);
      }

      if (kind == SyntaxKind.LessToken ||
          kind == SyntaxKind.LessOrEqualsToken ||
          kind == SyntaxKind.GreaterToken ||
          kind == SyntaxKind.GreaterOrEqualsToken)
      {
        return targetType == rightType && IsNumericType(targetType);
      }

      return targetType == rightType && IsNumericType(targetType);
    }

    private MethodGroupSymbol GetOrCreateUserMethodGroup(
        TypeSymbol type,
        string name)
    {
      if (!_methodGroupsByType.TryGetValue(type, out var groups))
      {
        groups = new Dictionary<string, MethodGroupSymbol>(StringComparer.Ordinal);
        _methodGroupsByType.Add(type, groups);
      }

      if (!groups.TryGetValue(name, out var group))
      {
        group = new MethodGroupSymbol(name, type);
        groups.Add(name, group);
      }

      return group;
    }

    private static bool HaveSameParameterTypes(
        IReadOnlyList<ParameterSymbol> left,
        IReadOnlyList<ParameterSymbol> right)
    {
      if (left.Count != right.Count)
        return false;

      for (var index = 0; index < left.Count; index++)
      {
        if (left[index].Type != right[index].Type)
          return false;
      }

      return true;
    }

    private void CollectFunctionSignature(FunctionDeclarationSyntax syntax)
    {
      if (syntax.OperatorToken != null || syntax.StaticKeyword != null)
      {
        Diagnostics.ReportInvalidOperatorName(
            GetFunctionNameSpan(syntax),
            syntax.Name);
        return;
      }

      var functionName = syntax.Name;
      var parameters = BindFunctionParameters(syntax.Parameters);
      var returnType = syntax.ReturnTypeAnnotation == null
          ? TypeSymbol.U0
          : BindTypeSyntax(syntax.ReturnTypeAnnotation.Type);
      var functionNameSpan = GetFunctionNameSpan(syntax);

      var functionSymbol = new FunctionSymbol(
          functionName,
          returnType,
          parameters,
          functionNameSpan,
          isPublic: syntax.PubKeyword != null,
          declaringModule: _currentModule?.LogicalName);
      _functionSymbolsBySyntax[syntax] = functionSymbol;

      if (_functionSymbols.ContainsKey(functionName))
      {
        Diagnostics.ReportDuplicateFunctionName(functionNameSpan, functionName);
        return;
      }

      _functionSymbols.Add(functionName, functionSymbol);
    }

    private IReadOnlyList<BoundStateDeclaration> BindStateDeclarations(
        IReadOnlyList<MemberSyntax> members)
    {
      var uniqueDeclarations = new List<StateDeclarationSyntax>();

      foreach (var member in members)
      {
        if (member is not StateDeclarationSyntax stateDeclaration)
          continue;

        var stateName = stateDeclaration.Identifier.Text ?? string.Empty;
        if (_stateSymbols.ContainsKey(stateName))
        {
          Diagnostics.ReportDuplicateState(
              stateDeclaration.Identifier.Span,
              stateName);
          continue;
        }

        if (_functionSymbols.ContainsKey(stateName))
        {
          Diagnostics.ReportStateNameConflict(
              stateDeclaration.Identifier.Span,
              stateName,
              "function");
        }

        var ordinal = uniqueDeclarations.Count;
        _stateSymbols.Add(
            stateName,
            new StateVariableSymbol(
                stateName,
                TypeSymbol.Error,
                false,
                false,
                null,
                null,
                stateDeclaration.Identifier.Span,
                stateDeclaration.Identifier.Span,
                ordinal));
        uniqueDeclarations.Add(stateDeclaration);
      }

      var states = new List<BoundStateDeclaration>(uniqueDeclarations.Count);
      for (var ordinal = 0; ordinal < uniqueDeclarations.Count; ordinal++)
      {
        var boundState = BindStateDeclaration(uniqueDeclarations[ordinal], ordinal);
        _stateSymbols[boundState.StateSymbol.Name] = boundState.StateSymbol;
        states.Add(boundState);
      }

      return states;
    }

    private BoundStateDeclaration BindStateDeclaration(
        StateDeclarationSyntax syntax,
        int ordinal)
    {
      var stateName = syntax.Identifier.Text ?? string.Empty;
      var declaredType = syntax.TypeClause != null
          ? BindTypeClause(syntax.TypeClause)
          : null;
      var synchronizationMode = BindSynchronizationMode(syntax.SynchronizationModifier);

      if (syntax.Initializer == null)
      {
        Diagnostics.ReportMissingStateInitializer(
            syntax.Identifier.Span,
            stateName);
        return CreateErrorStateDeclaration(syntax, ordinal, synchronizationMode);
      }

      var initializer = BindExpression(syntax.Initializer);
      var stateType = declaredType;
      if (stateType == null)
      {
        if (initializer.Type == TypeSymbol.Null || initializer.Type == TypeSymbol.Error)
        {
          Diagnostics.ReportCannotInferStateType(
              syntax.Identifier.Span,
              stateName);
          stateType = TypeSymbol.Error;
        }
        else
        {
          stateType = initializer.Type;
        }
      }
      else if (!CanAssignToLocal(stateType, initializer.Type))
      {
        Diagnostics.ReportTypeMismatch(
            GetExpressionSpan(syntax.Initializer),
            stateType.Name,
            initializer.Type.Name);
      }

      var isMutable = syntax.MutKeyword != null;
      if (synchronizationMode.HasValue && !isMutable)
      {
        Diagnostics.ReportSynchronizedStateMustBeMutable(
            syntax.SynchronizationModifier.SyncKeyword.Span,
            stateName);
      }

      if (synchronizationMode.HasValue &&
          stateType != TypeSymbol.Error &&
          !StateSynchronizationCompatibility.IsSupported(
              stateType,
              synchronizationMode.Value))
      {
        Diagnostics.ReportUnsupportedStateSynchronization(
            syntax.SynchronizationModifier.ModeToken?.Span ??
                syntax.SynchronizationModifier.SyncKeyword.Span,
            stateName,
            StateSynchronizationCompatibility.GetSourceName(synchronizationMode.Value),
            stateType.Name);
      }

      var hasConstantValue = TryEvaluateStateConstant(
          initializer,
          stateType,
          out var initialValue);
      if (!hasConstantValue)
      {
        Diagnostics.ReportStateInitializerMustBeConstant(
            GetExpressionSpan(syntax.Initializer),
            stateName);
      }

      var stateSymbol = new StateVariableSymbol(
          stateName,
          stateType ?? TypeSymbol.Error,
          isMutable,
          syntax.PubKeyword != null,
          synchronizationMode,
          initialValue,
          syntax.Identifier.Span,
          GetExpressionSpan(syntax.Initializer),
          ordinal);
      return new BoundStateDeclaration(stateSymbol, initializer);
    }

    private BoundStateDeclaration CreateErrorStateDeclaration(
        StateDeclarationSyntax syntax,
        int ordinal,
        StateSynchronizationMode? synchronizationMode)
    {
      var stateName = syntax.Identifier.Text ?? string.Empty;
      return new BoundStateDeclaration(
          new StateVariableSymbol(
              stateName,
              TypeSymbol.Error,
              syntax.MutKeyword != null,
              syntax.PubKeyword != null,
              synchronizationMode,
              null,
              syntax.Identifier.Span,
              syntax.Identifier.Span,
              ordinal),
          BoundErrorExpression.Instance);
    }

    private static StateSynchronizationMode? BindSynchronizationMode(
        SynchronizationModifierSyntax syntax)
    {
      if (syntax == null || syntax.Mode == SynchronizationModeSyntaxKind.Invalid)
        return null;

      return syntax.Mode switch
      {
        SynchronizationModeSyntaxKind.None => StateSynchronizationMode.None,
        SynchronizationModeSyntaxKind.Linear => StateSynchronizationMode.Linear,
        SynchronizationModeSyntaxKind.Smooth => StateSynchronizationMode.Smooth,
        _ => null
      };
    }

    private static bool TryEvaluateStateConstant(
        BoundExpression expression,
        TypeSymbol expectedType,
        out object value)
    {
      value = null;
      if (expression is BoundLiteralExpression literal)
      {
        if (literal.Type == TypeSymbol.Null)
          return expectedType != null && expectedType.IsReferenceType;

        if (literal.Type != expectedType)
          return false;

        value = literal.Value;
        return true;
      }

      if (expression is BoundBinaryExpression binary)
      {
        if (!TryEvaluateStateConstant(
                binary.Left,
                binary.Operator.LeftType,
                out var left) ||
            !TryEvaluateStateConstant(
                binary.Right,
                binary.Operator.RightType,
                out var right))
        {
          return false;
        }

        try
        {
          value = EvaluateBinaryConstant(binary.Operator.Kind, left, right);
          return value != null && binary.Type == expectedType;
        }
        catch (ArithmeticException)
        {
          return false;
        }
        catch (InvalidCastException)
        {
          return false;
        }
      }

      if (expression is not BoundUnaryExpression unary ||
          !TryEvaluateStateConstant(unary.Operand, unary.Operator.OperandType, out var operand))
      {
        return false;
      }

      try
      {
        switch (unary.Operator.Kind)
        {
          case BoundUnaryOperatorKind.Identity:
            value = operand;
            return true;

          case BoundUnaryOperatorKind.LogicalNegation when operand is bool boolean:
            value = !boolean;
            return true;

          case BoundUnaryOperatorKind.Negation:
            value = NegateConstant(operand);
            return value != null;

          case BoundUnaryOperatorKind.OnesComplement:
            value = ComplementConstant(operand);
            return value != null;
        }
      }
      catch (OverflowException)
      {
        return false;
      }

      return false;
    }

    private static object EvaluateBinaryConstant(
        BoundBinaryOperatorKind kind,
        object left,
        object right)
    {
      switch (kind)
      {
        case BoundBinaryOperatorKind.Equals:
          return Equals(left, right);
        case BoundBinaryOperatorKind.NotEquals:
          return !Equals(left, right);
        case BoundBinaryOperatorKind.LogicalAnd:
          return left is bool leftAnd && right is bool rightAnd
              ? leftAnd && rightAnd
              : null;
        case BoundBinaryOperatorKind.LogicalOr:
          return left is bool leftOr && right is bool rightOr
              ? leftOr || rightOr
              : null;
        case BoundBinaryOperatorKind.Less:
          return CompareConstants(left, right) < 0;
        case BoundBinaryOperatorKind.LessOrEquals:
          return CompareConstants(left, right) <= 0;
        case BoundBinaryOperatorKind.Greater:
          return CompareConstants(left, right) > 0;
        case BoundBinaryOperatorKind.GreaterOrEquals:
          return CompareConstants(left, right) >= 0;
      }

      return left switch
      {
        sbyte value => EvaluateInt8Constant(kind, value, (sbyte)right),
        byte value => EvaluateUInt8Constant(kind, value, (byte)right),
        short value => EvaluateInt16Constant(kind, value, (short)right),
        ushort value => EvaluateUInt16Constant(kind, value, (ushort)right),
        int value => EvaluateInt32Constant(kind, value, (int)right),
        uint value => EvaluateUInt32Constant(kind, value, (uint)right),
        long value => EvaluateInt64Constant(kind, value, (long)right),
        ulong value => EvaluateUInt64Constant(kind, value, (ulong)right),
        float value => EvaluateFloat32Constant(kind, value, (float)right),
        double value => EvaluateFloat64Constant(kind, value, (double)right),
        string value when kind == BoundBinaryOperatorKind.Addition => value + (string)right,
        _ => null
      };
    }

    private static int CompareConstants(object left, object right)
    {
      if (left is IComparable comparable && left.GetType() == right?.GetType())
        return comparable.CompareTo(right);

      throw new ArithmeticException("State constant operands are not comparable.");
    }

    private static object EvaluateInt8Constant(
        BoundBinaryOperatorKind kind,
        sbyte left,
        sbyte right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => checked((sbyte)(left + right)),
        BoundBinaryOperatorKind.Subtraction => checked((sbyte)(left - right)),
        BoundBinaryOperatorKind.Multiplication => checked((sbyte)(left * right)),
        BoundBinaryOperatorKind.Division => checked((sbyte)(left / right)),
        BoundBinaryOperatorKind.Modulus => checked((sbyte)(left % right)),
        BoundBinaryOperatorKind.BitwiseAnd => (sbyte)(left & right),
        BoundBinaryOperatorKind.BitwiseOr => (sbyte)(left | right),
        BoundBinaryOperatorKind.BitwiseXor => (sbyte)(left ^ right),
        BoundBinaryOperatorKind.LeftShift => checked((sbyte)(left << right)),
        BoundBinaryOperatorKind.RightShift => (sbyte)(left >> right),
        _ => null
      };
    }

    private static object EvaluateUInt8Constant(
        BoundBinaryOperatorKind kind,
        byte left,
        byte right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => checked((byte)(left + right)),
        BoundBinaryOperatorKind.Subtraction => checked((byte)(left - right)),
        BoundBinaryOperatorKind.Multiplication => checked((byte)(left * right)),
        BoundBinaryOperatorKind.Division => checked((byte)(left / right)),
        BoundBinaryOperatorKind.Modulus => checked((byte)(left % right)),
        BoundBinaryOperatorKind.BitwiseAnd => (byte)(left & right),
        BoundBinaryOperatorKind.BitwiseOr => (byte)(left | right),
        BoundBinaryOperatorKind.BitwiseXor => (byte)(left ^ right),
        BoundBinaryOperatorKind.LeftShift => checked((byte)(left << right)),
        BoundBinaryOperatorKind.RightShift => (byte)(left >> right),
        _ => null
      };
    }

    private static object EvaluateInt16Constant(
        BoundBinaryOperatorKind kind,
        short left,
        short right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => checked((short)(left + right)),
        BoundBinaryOperatorKind.Subtraction => checked((short)(left - right)),
        BoundBinaryOperatorKind.Multiplication => checked((short)(left * right)),
        BoundBinaryOperatorKind.Division => checked((short)(left / right)),
        BoundBinaryOperatorKind.Modulus => checked((short)(left % right)),
        BoundBinaryOperatorKind.BitwiseAnd => (short)(left & right),
        BoundBinaryOperatorKind.BitwiseOr => (short)(left | right),
        BoundBinaryOperatorKind.BitwiseXor => (short)(left ^ right),
        BoundBinaryOperatorKind.LeftShift => checked((short)(left << right)),
        BoundBinaryOperatorKind.RightShift => (short)(left >> right),
        _ => null
      };
    }

    private static object EvaluateUInt16Constant(
        BoundBinaryOperatorKind kind,
        ushort left,
        ushort right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => checked((ushort)(left + right)),
        BoundBinaryOperatorKind.Subtraction => checked((ushort)(left - right)),
        BoundBinaryOperatorKind.Multiplication => checked((ushort)(left * right)),
        BoundBinaryOperatorKind.Division => checked((ushort)(left / right)),
        BoundBinaryOperatorKind.Modulus => checked((ushort)(left % right)),
        BoundBinaryOperatorKind.BitwiseAnd => (ushort)(left & right),
        BoundBinaryOperatorKind.BitwiseOr => (ushort)(left | right),
        BoundBinaryOperatorKind.BitwiseXor => (ushort)(left ^ right),
        BoundBinaryOperatorKind.LeftShift => checked((ushort)(left << right)),
        BoundBinaryOperatorKind.RightShift => (ushort)(left >> right),
        _ => null
      };
    }

    private static object EvaluateInt32Constant(
        BoundBinaryOperatorKind kind,
        int left,
        int right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => checked(left + right),
        BoundBinaryOperatorKind.Subtraction => checked(left - right),
        BoundBinaryOperatorKind.Multiplication => checked(left * right),
        BoundBinaryOperatorKind.Division => left / right,
        BoundBinaryOperatorKind.Modulus => left % right,
        BoundBinaryOperatorKind.BitwiseAnd => left & right,
        BoundBinaryOperatorKind.BitwiseOr => left | right,
        BoundBinaryOperatorKind.BitwiseXor => left ^ right,
        BoundBinaryOperatorKind.LeftShift => left << right,
        BoundBinaryOperatorKind.RightShift => left >> right,
        _ => null
      };
    }

    private static object EvaluateUInt32Constant(
        BoundBinaryOperatorKind kind,
        uint left,
        uint right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => checked(left + right),
        BoundBinaryOperatorKind.Subtraction => checked(left - right),
        BoundBinaryOperatorKind.Multiplication => checked(left * right),
        BoundBinaryOperatorKind.Division => left / right,
        BoundBinaryOperatorKind.Modulus => left % right,
        BoundBinaryOperatorKind.BitwiseAnd => left & right,
        BoundBinaryOperatorKind.BitwiseOr => left | right,
        BoundBinaryOperatorKind.BitwiseXor => left ^ right,
        BoundBinaryOperatorKind.LeftShift => left << (int)right,
        BoundBinaryOperatorKind.RightShift => left >> (int)right,
        _ => null
      };
    }

    private static object EvaluateInt64Constant(
        BoundBinaryOperatorKind kind,
        long left,
        long right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => checked(left + right),
        BoundBinaryOperatorKind.Subtraction => checked(left - right),
        BoundBinaryOperatorKind.Multiplication => checked(left * right),
        BoundBinaryOperatorKind.Division => left / right,
        BoundBinaryOperatorKind.Modulus => left % right,
        BoundBinaryOperatorKind.BitwiseAnd => left & right,
        BoundBinaryOperatorKind.BitwiseOr => left | right,
        BoundBinaryOperatorKind.BitwiseXor => left ^ right,
        BoundBinaryOperatorKind.LeftShift => left << (int)right,
        BoundBinaryOperatorKind.RightShift => left >> (int)right,
        _ => null
      };
    }

    private static object EvaluateUInt64Constant(
        BoundBinaryOperatorKind kind,
        ulong left,
        ulong right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => checked(left + right),
        BoundBinaryOperatorKind.Subtraction => checked(left - right),
        BoundBinaryOperatorKind.Multiplication => checked(left * right),
        BoundBinaryOperatorKind.Division => left / right,
        BoundBinaryOperatorKind.Modulus => left % right,
        BoundBinaryOperatorKind.BitwiseAnd => left & right,
        BoundBinaryOperatorKind.BitwiseOr => left | right,
        BoundBinaryOperatorKind.BitwiseXor => left ^ right,
        BoundBinaryOperatorKind.LeftShift => left << (int)right,
        BoundBinaryOperatorKind.RightShift => left >> (int)right,
        _ => null
      };
    }

    private static object EvaluateFloat32Constant(
        BoundBinaryOperatorKind kind,
        float left,
        float right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => left + right,
        BoundBinaryOperatorKind.Subtraction => left - right,
        BoundBinaryOperatorKind.Multiplication => left * right,
        BoundBinaryOperatorKind.Division => left / right,
        BoundBinaryOperatorKind.Modulus => left % right,
        _ => null
      };
    }

    private static object EvaluateFloat64Constant(
        BoundBinaryOperatorKind kind,
        double left,
        double right)
    {
      return kind switch
      {
        BoundBinaryOperatorKind.Addition => left + right,
        BoundBinaryOperatorKind.Subtraction => left - right,
        BoundBinaryOperatorKind.Multiplication => left * right,
        BoundBinaryOperatorKind.Division => left / right,
        BoundBinaryOperatorKind.Modulus => left % right,
        _ => null
      };
    }

    private static object NegateConstant(object value)
    {
      return value switch
      {
        sbyte number => checked((sbyte)-number),
        short number => checked((short)-number),
        int number => checked(-number),
        long number => checked(-number),
        float number => -number,
        double number => -number,
        _ => null
      };
    }

    private static object ComplementConstant(object value)
    {
      return value switch
      {
        sbyte number => (sbyte)~number,
        byte number => (byte)~number,
        short number => (short)~number,
        ushort number => (ushort)~number,
        int number => ~number,
        uint number => ~number,
        long number => ~number,
        ulong number => ~number,
        _ => null
      };
    }

    private IReadOnlyList<ParameterSymbol> BindFunctionParameters(
        IReadOnlyList<ParameterSyntax> parameterSyntaxes)
    {
      var parameters = new List<ParameterSymbol>();
      var seenParameterNames = new HashSet<string>(StringComparer.Ordinal);

      for (var index = 0; index < parameterSyntaxes.Count; index++)
      {
        var parameterSyntax = parameterSyntaxes[index];
        var parameterName = parameterSyntax.Identifier.Text ?? string.Empty;
        if (!seenParameterNames.Add(parameterName))
          Diagnostics.ReportDuplicateParameterName(parameterSyntax.Identifier.Span, parameterName);

        var parameterType = BindTypeSyntax(parameterSyntax.Type);
        parameters.Add(new ParameterSymbol(
            parameterName,
            parameterType,
            index,
            parameterName,
            parameterSyntax.Identifier.Span));
      }

      return parameters;
    }

    private BoundFunctionDeclaration BindFunctionDeclaration(
        FunctionDeclarationSyntax syntax,
        FunctionSymbol functionSymbol)
    {
      var body = BindFunctionBody(syntax.Body, functionSymbol, out var sawValueReturn);

      if (functionSymbol.ReturnType != TypeSymbol.U0 &&
          !sawValueReturn &&
          GetBlockFallthroughType(body) != TypeSymbol.Never)
      {
        Diagnostics.ReportReturnValueRequired(
            functionSymbol.SourceSpan,
            functionSymbol.Name,
            functionSymbol.ReturnType.Name);
      }

      return new BoundFunctionDeclaration(functionSymbol, body);
    }

    private BoundBlockStatement BindFunctionBody(
        BlockStatementSyntax syntax,
        FunctionSymbol functionSymbol,
        out bool sawValueReturn)
    {
      var parentScope = _scope;
      var previousReturnType = _currentReturnType;
      var previousEventName = _currentEventName;
      var previousSawValueReturn = _sawValueReturn;
      var previousType = _currentType;
      var previousFunction = _currentFunction;

      _scope = new BoundScope(parentScope);
      foreach (var parameter in functionSymbol.Parameters)
        _scope.DeclareParameter(parameter);
      if (functionSymbol.SelfParameter != null)
        _scope.DeclareParameter(functionSymbol.SelfParameter);

      _currentReturnType = functionSymbol.ReturnType;
      _currentEventName = functionSymbol.Name;
      _currentType = functionSymbol.ContainingType;
      _currentFunction = functionSymbol;
      _sawValueReturn = false;

      try
      {
        var body = BindBlockStatement(syntax);
        sawValueReturn = _sawValueReturn;
        return body;
      }
      finally
      {
        _scope = parentScope;
        _currentReturnType = previousReturnType;
        _currentEventName = previousEventName;
        _sawValueReturn = previousSawValueReturn;
        _currentType = previousType;
        _currentFunction = previousFunction;
      }
    }

    private void ReportRecursiveFunctions(IReadOnlyList<BoundFunctionDeclaration> functions)
    {
      var declarations = new Dictionary<FunctionSymbol, BoundFunctionDeclaration>();
      var graph = new Dictionary<FunctionSymbol, HashSet<FunctionSymbol>>();

      foreach (var function in functions)
      {
        declarations[function.FunctionSymbol] = function;
        var callees = new HashSet<FunctionSymbol>();
        CollectFunctionCallees(function.Body, callees);
        graph[function.FunctionSymbol] = callees;
      }

      var states = new Dictionary<FunctionSymbol, int>();
      var stack = new List<FunctionSymbol>();
      var reported = new HashSet<FunctionSymbol>();

      foreach (var function in functions)
        VisitFunctionForRecursion(function.FunctionSymbol, declarations, graph, states, stack, reported);
    }

    private void VisitFunctionForRecursion(
        FunctionSymbol function,
        IReadOnlyDictionary<FunctionSymbol, BoundFunctionDeclaration> declarations,
        IReadOnlyDictionary<FunctionSymbol, HashSet<FunctionSymbol>> graph,
        IDictionary<FunctionSymbol, int> states,
        IList<FunctionSymbol> stack,
        ISet<FunctionSymbol> reported)
    {
      if (states.TryGetValue(function, out var state))
      {
        if (state == 2)
          return;
      }

      states[function] = 1;
      stack.Add(function);

      if (graph.TryGetValue(function, out var callees))
      {
        foreach (var callee in callees)
        {
          if (!declarations.ContainsKey(callee))
            continue;

          if (!states.TryGetValue(callee, out var calleeState))
          {
            VisitFunctionForRecursion(callee, declarations, graph, states, stack, reported);
            continue;
          }

          if (calleeState == 1)
            ReportFunctionCycle(callee, stack, reported);
        }
      }

      stack.RemoveAt(stack.Count - 1);
      states[function] = 2;
    }

    private void ReportFunctionCycle(
        FunctionSymbol cycleStart,
        IList<FunctionSymbol> stack,
        ISet<FunctionSymbol> reported)
    {
      var startIndex = -1;
      for (var index = 0; index < stack.Count; index++)
      {
        if (ReferenceEquals(stack[index], cycleStart))
        {
          startIndex = index;
          break;
        }
      }

      if (startIndex < 0)
        return;

      var cycleNames = new List<string>();
      for (var index = startIndex; index < stack.Count; index++)
        cycleNames.Add(stack[index].Name);
      cycleNames.Add(cycleStart.Name);

      var cycleDisplay = string.Join(" -> ", cycleNames);
      for (var index = startIndex; index < stack.Count; index++)
      {
        var function = stack[index];
        if (!reported.Add(function))
          continue;

        var previousSourcePath = Diagnostics.SourcePath;
        if (_modulesByFunctionSymbol.TryGetValue(function, out var module))
          Diagnostics.SourcePath = module.SourcePath;

        Diagnostics.ReportRecursiveFunction(
            function.SourceSpan,
            function.Name,
            cycleDisplay);
        Diagnostics.SourcePath = previousSourcePath;
      }
    }

    private static void CollectFunctionCallees(
        BoundStatement statement,
        ISet<FunctionSymbol> callees)
    {
      switch (statement)
      {
        case BoundBlockStatement blockStatement:
          foreach (var child in blockStatement.Statements)
            CollectFunctionCallees(child, callees);
          return;

        case BoundVariableDeclarationStatement variableDeclaration:
          CollectFunctionCallees(variableDeclaration.Initializer, callees);
          return;

        case BoundExpressionStatement expressionStatement:
          CollectFunctionCallees(expressionStatement.Expression, callees);
          return;

        case BoundReturnStatement returnStatement:
          if (returnStatement.Expression != null)
            CollectFunctionCallees(returnStatement.Expression, callees);
          return;

        case BoundBreakStatement breakStatement:
          if (breakStatement.Expression != null)
            CollectFunctionCallees(breakStatement.Expression, callees);
          return;
      }
    }

    private static void CollectFunctionCallees(
        BoundExpression expression,
        ISet<FunctionSymbol> callees)
    {
      switch (expression)
      {
        case BoundUserFunctionCallExpression functionCall:
          callees.Add(functionCall.Function);
          if (functionCall.Receiver != null)
            CollectFunctionCallees(functionCall.Receiver, callees);
          foreach (var argument in functionCall.Arguments)
            CollectFunctionCallees(argument, callees);
          return;

        case BoundCallExpression callExpression:
          if (callExpression.Target != null)
            CollectFunctionCallees(callExpression.Target, callees);
          foreach (var argument in callExpression.Arguments)
            CollectFunctionCallees(argument, callees);
          return;

        case BoundUnaryExpression unaryExpression:
          CollectFunctionCallees(unaryExpression.Operand, callees);
          return;

        case BoundBinaryExpression binaryExpression:
          CollectFunctionCallees(binaryExpression.Left, callees);
          CollectFunctionCallees(binaryExpression.Right, callees);
          return;

        case BoundAssignmentExpression assignmentExpression:
          CollectFunctionCallees(assignmentExpression.Expression, callees);
          return;

        case BoundArrayLiteralExpression arrayLiteralExpression:
          foreach (var element in arrayLiteralExpression.Elements)
            CollectFunctionCallees(element, callees);
          return;

        case BoundMemberAccessExpression memberAccessExpression:
          if (memberAccessExpression.Receiver != null)
            CollectFunctionCallees(memberAccessExpression.Receiver, callees);
          return;

        case BoundBlockExpression blockExpression:
          CollectFunctionCallees(blockExpression.Block, callees);
          if (blockExpression.TrailingExpression != null)
            CollectFunctionCallees(blockExpression.TrailingExpression, callees);
          return;

        case BoundIfExpression ifExpression:
          CollectFunctionCallees(ifExpression.Condition, callees);
          CollectFunctionCallees(ifExpression.ThenExpression, callees);
          if (ifExpression.ElseExpression != null)
            CollectFunctionCallees(ifExpression.ElseExpression, callees);
          return;

        case BoundWhileExpression whileExpression:
          CollectFunctionCallees(whileExpression.Condition, callees);
          CollectFunctionCallees(whileExpression.Body, callees);
          return;

        case BoundLoopExpression loopExpression:
          CollectFunctionCallees(loopExpression.Body, callees);
          return;
      }
    }

    private BoundEventDeclaration BindEventDeclaration(
        EventDeclarationSyntax syntax,
        ISet<string> declaredEvents)
    {
      var eventName = syntax.Identifier.Text ?? "";
      EventCatalog.TryGet(eventName, out var definition);

      if (definition == null)
      {
        Diagnostics.ReportUnknownEvent(syntax.Identifier.Span, eventName);
        definition = CreateErrorEventDefinition(eventName);
      }
      else if (definition.SupportLevel == EventSupportLevel.PendingSignature ||
               definition.SupportLevel == EventSupportLevel.Unsupported)
      {
        Diagnostics.ReportUnsupportedEventSignature(syntax.Identifier.Span, eventName);
      }

      if (!declaredEvents.Add(eventName))
        Diagnostics.ReportDuplicateEvent(syntax.Identifier.Span, eventName);

      var parameters = BindEventParameters(syntax, definition);
      var returnType = BindEventReturnType(syntax, definition);

      if (!string.IsNullOrWhiteSpace(definition.Requirement))
      {
        Diagnostics.ReportEventRequiresComponent(
            syntax.Identifier.Span,
            eventName,
            definition.Requirement);
      }

      var eventSymbol = new BoundEventSymbol(
          eventName,
          definition.UdonName,
          returnType,
          parameters,
          definition.Category,
          definition.Requirement,
          definition.SupportLevel,
          syntax.Identifier.Span,
          definition.ReturnValueStorageName);

      var body = BindEventBody(syntax.Body, eventSymbol, out var sawValueReturn);

      if (eventSymbol.ReturnType != TypeSymbol.U0 &&
          !sawValueReturn &&
          GetBlockFallthroughType(body) != TypeSymbol.Never)
      {
        Diagnostics.ReportReturnValueRequired(
            syntax.Identifier.Span,
            eventName,
            eventSymbol.ReturnType.Name);
      }

      return new BoundEventDeclaration(eventSymbol, body);
    }

    private static EventDefinition CreateErrorEventDefinition(string eventName)
    {
      return new EventDefinition(
          eventName,
          "_invalid_event",
          EventCategory.UdonInput,
          TypeSymbol.U0,
          Array.Empty<EventParameterDefinition>(),
          null,
          EventSupportLevel.Unsupported);
    }

    private IReadOnlyList<ParameterSymbol> BindEventParameters(
        EventDeclarationSyntax syntax,
        EventDefinition definition)
    {
      var parameters = new List<ParameterSymbol>();
      var seenParameterNames = new HashSet<string>(StringComparer.Ordinal);

      if (definition.SupportLevel == EventSupportLevel.Supported &&
          syntax.Parameters.Count != definition.Parameters.Count)
      {
        Diagnostics.ReportEventParameterCountMismatch(
            syntax.Identifier.Span,
            definition.SourceName,
            definition.Parameters.Count,
            syntax.Parameters.Count);
      }

      for (var index = 0; index < syntax.Parameters.Count; index++)
      {
        var parameterSyntax = syntax.Parameters[index];
        var parameterName = parameterSyntax.Identifier.Text ?? string.Empty;
        if (!seenParameterNames.Add(parameterName))
          Diagnostics.ReportDuplicateParameterName(parameterSyntax.Identifier.Span, parameterName);

        var parameterType = BindTypeSyntax(parameterSyntax.Type);
        var udonStorageName = parameterName;

        if (definition.SupportLevel == EventSupportLevel.Supported &&
            index < definition.Parameters.Count)
        {
          var expectedParameter = definition.Parameters[index];
          udonStorageName = expectedParameter.UdonStorageName;

          if (parameterType != TypeSymbol.Error &&
              parameterType != expectedParameter.Type)
          {
            Diagnostics.ReportEventParameterTypeMismatch(
                parameterSyntax.Type.GetSpan(),
                definition.SourceName,
                index,
                expectedParameter.Type.Name,
                parameterType.Name);
          }
        }

        parameters.Add(new ParameterSymbol(
            parameterName,
            parameterType,
            index,
            udonStorageName,
            parameterSyntax.Identifier.Span));
      }

      return parameters;
    }

    private TypeSymbol BindEventReturnType(
        EventDeclarationSyntax syntax,
        EventDefinition definition)
    {
      if (syntax.ReturnTypeAnnotation == null)
      {
        if (definition.ReturnType != TypeSymbol.U0 &&
            definition.SupportLevel == EventSupportLevel.Supported)
        {
          Diagnostics.ReportEventReturnTypeRequired(
              syntax.Identifier.Span,
              definition.SourceName,
              definition.ReturnType.Name);
        }

        return definition.ReturnType;
      }

      var declaredReturnType = BindTypeClause(syntax.ReturnTypeAnnotation);
      if (definition.SupportLevel != EventSupportLevel.Supported)
        return declaredReturnType;

      if (definition.SupportLevel == EventSupportLevel.Supported &&
          declaredReturnType != TypeSymbol.Error &&
          declaredReturnType != definition.ReturnType)
      {
        Diagnostics.ReportEventReturnTypeMismatch(
            syntax.ReturnTypeAnnotation.Type.GetSpan(),
            definition.SourceName,
            definition.ReturnType.Name,
            declaredReturnType.Name);
      }

      return definition.ReturnType;
    }

    private BoundBlockStatement BindEventBody(
        BlockStatementSyntax syntax,
        BoundEventSymbol eventSymbol,
        out bool sawValueReturn)
    {
      var parentScope = _scope;
      var previousReturnType = _currentReturnType;
      var previousEventName = _currentEventName;
      var previousSawValueReturn = _sawValueReturn;

      _scope = new BoundScope(parentScope);
      foreach (var parameter in eventSymbol.Parameters)
        _scope.DeclareParameter(parameter);

      _currentReturnType = eventSymbol.ReturnType;
      _currentEventName = eventSymbol.SourceName;
      _sawValueReturn = false;

      try
      {
        var body = BindBlockStatement(syntax);
        sawValueReturn = _sawValueReturn;
        return body;
      }
      finally
      {
        _scope = parentScope;
        _currentReturnType = previousReturnType;
        _currentEventName = previousEventName;
        _sawValueReturn = previousSawValueReturn;
      }
    }

    private BoundBlockStatement BindBlockStatement(BlockStatementSyntax syntax)
    {
      var statements = new List<BoundStatement>();
      var parentScope = _scope;
      _scope = new BoundScope(parentScope);

      try
      {
        foreach (var statement in syntax.Statements)
          statements.Add(BindStatement(statement));

        if (syntax.TrailingExpression != null)
          BindTrailingExpression(syntax.TrailingExpression, statements);
      }
      finally
      {
        _scope = parentScope;
      }

      return new BoundBlockStatement(statements);
    }

    private BoundBlockExpression BindBlockExpression(BlockStatementSyntax syntax)
    {
      var statements = new List<BoundStatement>();
      BoundExpression trailingExpression = null;
      var parentScope = _scope;
      _scope = new BoundScope(parentScope);

      try
      {
        foreach (var statement in syntax.Statements)
          statements.Add(BindStatement(statement));

        if (syntax.TrailingExpression != null)
          trailingExpression = BindExpression(syntax.TrailingExpression);
      }
      finally
      {
        _scope = parentScope;
      }

      var block = new BoundBlockStatement(statements);
      var type = trailingExpression?.Type ?? GetBlockFallthroughType(block);
      return new BoundBlockExpression(block, trailingExpression, type);
    }

    private static TypeSymbol GetBlockFallthroughType(BoundBlockStatement block)
    {
      if (block.Statements.Count == 0)
        return TypeSymbol.U0;

      var lastStatement = block.Statements[^1];
      if (lastStatement is BoundReturnStatement ||
          lastStatement is BoundBreakStatement ||
          lastStatement is BoundContinueStatement ||
          lastStatement is BoundRedoStatement)
      {
        return TypeSymbol.Never;
      }

      if (lastStatement is BoundExpressionStatement expressionStatement &&
          expressionStatement.Expression.Type == TypeSymbol.Never)
      {
        return TypeSymbol.Never;
      }

      if (lastStatement is BoundBlockStatement nestedBlock)
        return GetBlockFallthroughType(nestedBlock);

      return TypeSymbol.U0;
    }

    private void BindTrailingExpression(
        ExpressionSyntax syntax,
        IList<BoundStatement> statements)
    {
      var expression = BindExpression(syntax);

      if (_currentReturnType == TypeSymbol.U0)
      {
        if (expression.Type != TypeSymbol.Error &&
            expression.Type != TypeSymbol.U0 &&
            expression.Type != TypeSymbol.Never)
        {
          Diagnostics.ReportReturnValueNotAllowed(
              GetExpressionSpan(syntax),
              _currentEventName);
        }

        statements.Add(new BoundExpressionStatement(expression));
        return;
      }

      _sawValueReturn = true;
      if (expression.Type != TypeSymbol.Error &&
          expression.Type != TypeSymbol.Never &&
          expression.Type != _currentReturnType)
      {
        Diagnostics.ReportReturnTypeMismatch(
            GetExpressionSpan(syntax),
            _currentReturnType.Name,
            expression.Type.Name);
      }

      statements.Add(new BoundReturnStatement(expression));
    }

    private BoundStatement BindStatement(StatementSyntax syntax)
    {
      if (syntax is VariableDeclarationStatementSyntax variableDeclarationStatement)
        return BindVariableDeclarationStatement(variableDeclarationStatement);

      if (syntax is ReturnStatementSyntax returnStatement)
        return BindReturnStatement(returnStatement);

      if (syntax is BreakStatementSyntax breakStatement)
        return BindBreakStatement(breakStatement);

      if (syntax is ContinueStatementSyntax continueStatement)
        return BindContinueStatement(continueStatement);

      if (syntax is RedoStatementSyntax redoStatement)
        return BindRedoStatement(redoStatement);

      if (syntax is ExpressionStatementSyntax expressionStatement)
      {
        return new BoundExpressionStatement(
            BindExpression(expressionStatement.Expression));
      }

      if (syntax is BlockStatementSyntax blockStatement)
        return BindBlockStatement(blockStatement);

      Diagnostics.ReportUnsupportedStatement(
          GetStatementSpan(syntax),
          syntax.GetType().Name);
      return new BoundExpressionStatement(BoundErrorExpression.Instance);
    }

    private BoundStatement BindBreakStatement(BreakStatementSyntax syntax)
    {
      var expression = syntax.Expression == null
          ? null
          : BindExpression(syntax.Expression);
      var target = ResolveLoopTarget(
          syntax.Label,
          syntax.BreakKeyword,
          "break");
      if (target == null)
        return new BoundExpressionStatement(BoundErrorExpression.Instance);

      if (target.Symbol.IsWhile && expression != null)
      {
        Diagnostics.ReportBreakValueTargetsWhile(
            GetExpressionSpan(syntax.Expression));
        return new BoundBreakStatement(target.Symbol, expression);
      }

      if (!target.Symbol.IsWhile)
        RegisterLoopBreak(target, expression, syntax);

      return new BoundBreakStatement(target.Symbol, expression);
    }

    private BoundStatement BindContinueStatement(ContinueStatementSyntax syntax)
    {
      var target = ResolveLoopTarget(
          syntax.Label,
          syntax.ContinueKeyword,
          "continue");
      if (target == null)
        return new BoundExpressionStatement(BoundErrorExpression.Instance);

      return new BoundContinueStatement(target.Symbol);
    }

    private BoundStatement BindRedoStatement(RedoStatementSyntax syntax)
    {
      var target = ResolveLoopTarget(
          syntax.Label,
          syntax.RedoKeyword,
          "redo");
      if (target == null)
        return new BoundExpressionStatement(BoundErrorExpression.Instance);

      return new BoundRedoStatement(target.Symbol);
    }

    private LoopBindingContext ResolveLoopTarget(
        SyntaxToken label,
        SyntaxToken keyword,
        string statementName)
    {
      if (_loopContexts.Count == 0)
      {
        Diagnostics.ReportJumpOutsideLoop(keyword.Span, statementName);
        return null;
      }

      if (label == null)
        return _loopContexts[^1];

      var labelName = GetLabelName(label);
      for (var index = _loopContexts.Count - 1; index >= 0; index--)
      {
        if (string.Equals(
            _loopContexts[index].Symbol.Label,
            labelName,
            StringComparison.Ordinal))
        {
          return _loopContexts[index];
        }
      }

      Diagnostics.ReportUnknownLoopLabel(label.Span, labelName);
      return null;
    }

    private void RegisterLoopBreak(
        LoopBindingContext target,
        BoundExpression expression,
        BreakStatementSyntax syntax)
    {
      if (expression != null &&
          (expression.Type == TypeSymbol.Error ||
           expression.Type == TypeSymbol.Never))
      {
        return;
      }

      target.HasReachableBreak = true;
      var breakKind = expression == null
          ? LoopBreakKind.Empty
          : LoopBreakKind.Value;

      if (target.BreakKind == LoopBreakKind.None)
      {
        target.BreakKind = breakKind;
        target.BreakType = expression?.Type;
        return;
      }

      if (target.BreakKind != breakKind)
      {
        Diagnostics.ReportMixedLoopBreakValues(
            syntax.BreakKeyword.Span,
            target.Symbol.Label);
        return;
      }

      if (breakKind == LoopBreakKind.Value &&
          target.BreakType != expression.Type)
      {
        Diagnostics.ReportLoopBreakTypeMismatch(
            GetExpressionSpan(syntax.Expression),
            target.BreakType.Name,
            expression.Type.Name);
      }
    }

    private BoundReturnStatement BindReturnStatement(ReturnStatementSyntax syntax)
    {
      if (_currentReturnType == TypeSymbol.U0)
      {
        if (syntax.Expression != null)
        {
          var expression = BindExpression(syntax.Expression);
          Diagnostics.ReportReturnValueNotAllowed(
              GetExpressionSpan(syntax.Expression),
              _currentEventName);
          return new BoundReturnStatement(expression);
        }

        return new BoundReturnStatement(null);
      }

      if (syntax.Expression == null)
      {
        Diagnostics.ReportReturnValueRequired(
            syntax.ReturnKeyword.Span,
            _currentEventName,
            _currentReturnType.Name);
        return new BoundReturnStatement(BoundErrorExpression.Instance);
      }

      var returnExpression = BindExpression(syntax.Expression);
      _sawValueReturn = true;
      if (returnExpression.Type != TypeSymbol.Error &&
          returnExpression.Type != TypeSymbol.Never &&
          returnExpression.Type != _currentReturnType)
      {
        Diagnostics.ReportReturnTypeMismatch(
            GetExpressionSpan(syntax.Expression),
            _currentReturnType.Name,
            returnExpression.Type.Name);
      }

      return new BoundReturnStatement(returnExpression);
    }

    private BoundVariableDeclarationStatement BindVariableDeclarationStatement(
        VariableDeclarationStatementSyntax syntax)
    {
      var variableName = syntax.Identifier.Text ?? string.Empty;
      var declaredType = syntax.TypeClause != null
          ? BindTypeClause(syntax.TypeClause)
          : null;

      if (syntax.Initializer == null)
      {
        Diagnostics.ReportMissingVariableInitializer(
            syntax.Identifier.Span,
            variableName);

        return CreateErrorVariableDeclaration(variableName, syntax.Identifier.Span);
      }

      var initializer = BindExpression(syntax.Initializer);
      var variableType = declaredType;

      if (variableType == null)
      {
        if (initializer.Type == TypeSymbol.Null)
        {
          Diagnostics.ReportCannotInferVariableType(
              syntax.Identifier.Span,
              variableName);
          return CreateErrorVariableDeclaration(variableName, syntax.Identifier.Span);
        }

        variableType = initializer.Type;
      }
      else if (!CanAssignToLocal(variableType, initializer.Type))
      {
        Diagnostics.ReportTypeMismatch(
            GetExpressionSpan(syntax.Initializer),
            variableType.Name,
            initializer.Type.Name);
      }

      if (variableType == null || variableType == TypeSymbol.Error)
        return CreateErrorVariableDeclaration(variableName, syntax.Identifier.Span);

      var local = new LocalVariableSymbol(
          variableName,
          variableType,
          syntax.MutKeyword != null,
          syntax.Identifier.Span);

      _scope?.Declare(local);
      return new BoundVariableDeclarationStatement(local, initializer);
    }

    private BoundVariableDeclarationStatement CreateErrorVariableDeclaration(
        string variableName,
        TextSpan declarationSpan)
    {
      return new BoundVariableDeclarationStatement(
          new LocalVariableSymbol(
              variableName,
              TypeSymbol.Error,
              false,
              declarationSpan),
          BoundErrorExpression.Instance);
    }

    private TypeSymbol BindTypeClause(TypeClauseSyntax syntax)
    {
      return BindTypeSyntax(syntax.Type);
    }

    private TypeSymbol BindTypeSyntax(TypeSyntax syntax)
    {
      var typeName = syntax.GetText();
      if (string.Equals(typeName, "Self", StringComparison.Ordinal))
      {
        if (_currentType != null)
          return _currentType;

        Diagnostics.ReportSelfTypeOutsideImpl(syntax.GetSpan());
        return TypeSymbol.Error;
      }

      if (BuiltInTypes.TryGetValue(typeName, out var builtInType))
        return builtInType;

      if (_declaredTypes.TryGetValue(typeName, out var declaredType))
        return declaredType;

      if (EventCatalog.TryGetKnownType(typeName, out var eventType))
        return ResolveCanonicalType(eventType);

      var span = syntax.GetSpan();
      if (typeName.IndexOf('.', StringComparison.Ordinal) >= 0)
      {
        if (_environment.ExternCatalog.TryGetTypeSymbol(typeName, out var qualifiedTypeSymbol))
          return qualifiedTypeSymbol;

        Diagnostics.ReportUnknownType(span, typeName);
        return TypeSymbol.Error;
      }

      var resolvedSymbol = ResolveVisibleSymbol(
          typeName,
          span,
          out var resolutionHadDiagnostic);
      if (resolvedSymbol is TypeSymbol typeSymbol)
        return typeSymbol;

      if (resolutionHadDiagnostic)
        return TypeSymbol.Error;

      Diagnostics.ReportUnknownType(span, typeName);
      return TypeSymbol.Error;
    }

    private TypeSymbol ResolveCanonicalType(TypeSymbol type)
    {
      if (_environment.ExternCatalog.TryGetTypeSymbol(type.QualifiedName, out var environmentType))
        return environmentType;

      return type;
    }

    private BoundExpression BindExpression(ExpressionSyntax syntax)
    {
      if (syntax is AssignmentExpressionSyntax assignmentExpression)
        return BindAssignmentExpression(assignmentExpression);

      if (syntax is ParenthesizedExpressionSyntax parenthesizedExpression)
        return BindExpression(parenthesizedExpression.Expression);

      if (syntax is UnaryExpressionSyntax unaryExpression)
        return BindUnaryExpression(unaryExpression);

      if (syntax is BinaryExpressionSyntax binaryExpression)
        return BindBinaryExpression(binaryExpression);

      if (syntax is IfExpressionSyntax ifExpression)
        return BindIfExpression(ifExpression);

      if (syntax is WhileExpressionSyntax whileExpression)
        return BindWhileExpression(whileExpression);

      if (syntax is LoopExpressionSyntax loopExpression)
        return BindLoopExpression(loopExpression);

      if (syntax is BlockExpressionSyntax blockExpression)
        return BindBlockExpression(blockExpression.Block);

      if (syntax is StringLiteralExpressionSyntax stringLiteralExpression)
        return BindStringLiteralExpression(stringLiteralExpression);

      if (syntax is IntegerLiteralExpressionSyntax integerLiteralExpression)
        return BindIntegerLiteralExpression(integerLiteralExpression);

      if (syntax is FloatLiteralExpressionSyntax floatLiteralExpression)
        return BindFloatLiteralExpression(floatLiteralExpression);

      if (syntax is CharacterLiteralExpressionSyntax characterLiteralExpression)
        return BindCharacterLiteralExpression(characterLiteralExpression);

      if (syntax is BooleanLiteralExpressionSyntax booleanLiteralExpression)
        return BindBooleanLiteralExpression(booleanLiteralExpression);

      if (syntax is NullLiteralExpressionSyntax nullLiteralExpression)
        return new BoundLiteralExpression(null, TypeSymbol.Null, nullLiteralExpression.NullToken.Span);

      if (syntax is ArrayLiteralExpressionSyntax arrayLiteralExpression)
        return BindArrayLiteralExpression(arrayLiteralExpression);

      if (syntax is NameExpressionSyntax nameExpression)
        return BindNameExpression(nameExpression);

      if (syntax is MemberAccessExpressionSyntax memberAccessExpression)
        return BindMemberAccessExpression(memberAccessExpression);

      if (syntax is CallExpressionSyntax callExpression)
        return BindCallExpression(callExpression);

      if (syntax is ExternExpressionSyntax externExpression)
        return BindExternExpression(externExpression);

      Diagnostics.ReportUnsupportedExpression(
          GetExpressionSpan(syntax),
          syntax.GetType().Name);
      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindIfExpression(IfExpressionSyntax syntax)
    {
      var condition = BindExpression(syntax.Condition);
      RequireBoolCondition(condition, syntax.Condition, "if");

      var thenExpression = BindBlockExpression(syntax.ThenBlock);
      BoundExpression elseExpression = null;
      if (syntax.ElseExpression != null)
        elseExpression = BindExpression(syntax.ElseExpression);

      TypeSymbol resultType;
      if (elseExpression == null)
      {
        resultType = TypeSymbol.U0;
        if (thenExpression.Type != TypeSymbol.Error &&
            thenExpression.Type != TypeSymbol.U0 &&
            thenExpression.Type != TypeSymbol.Never)
        {
          Diagnostics.ReportIfValueRequiresElse(
              GetExpressionSpan(syntax));
        }
      }
      else
      {
        resultType = UnifyIfBranchTypes(
            thenExpression.Type,
            elseExpression.Type,
            GetExpressionSpan(syntax.ElseExpression));
      }

      return new BoundIfExpression(
          condition,
          thenExpression,
          elseExpression,
          resultType);
    }

    private BoundExpression BindWhileExpression(WhileExpressionSyntax syntax)
    {
      var condition = BindExpression(syntax.Condition);
      RequireBoolCondition(condition, syntax.Condition, "while");

      var context = EnterLoop(
          syntax.Label,
          isWhile: true,
          syntax.WhileKeyword.Span);
      BoundBlockExpression body;
      try
      {
        body = BindBlockExpression(syntax.Body);
      }
      finally
      {
        ExitLoop(context);
      }

      return new BoundWhileExpression(context.Symbol, condition, body);
    }

    private BoundExpression BindLoopExpression(LoopExpressionSyntax syntax)
    {
      var context = EnterLoop(
          syntax.Label,
          isWhile: false,
          syntax.LoopKeyword.Span);
      BoundBlockExpression body;
      try
      {
        body = BindBlockExpression(syntax.Body);
      }
      finally
      {
        ExitLoop(context);
      }

      var resultType = !context.HasReachableBreak
          ? TypeSymbol.Never
          : context.BreakKind == LoopBreakKind.Value
              ? context.BreakType ?? TypeSymbol.Error
              : TypeSymbol.U0;
      return new BoundLoopExpression(context.Symbol, body, resultType);
    }

    private LoopBindingContext EnterLoop(
        LoopLabelSyntax labelSyntax,
        bool isWhile,
        TextSpan keywordSpan)
    {
      var label = labelSyntax == null
          ? null
          : GetLabelName(labelSyntax.LabelToken);
      if (!string.IsNullOrEmpty(label))
      {
        foreach (var activeLoop in _loopContexts)
        {
          if (!string.Equals(
              activeLoop.Symbol.Label,
              label,
              StringComparison.Ordinal))
          {
            continue;
          }

          Diagnostics.ReportDuplicateLoopLabel(
              labelSyntax.LabelToken.Span,
              label);
          break;
        }
      }

      var span = labelSyntax?.LabelToken.Span ?? keywordSpan;
      var context = new LoopBindingContext(
          new LoopSymbol(label, isWhile, span));
      _loopContexts.Add(context);
      return context;
    }

    private void ExitLoop(LoopBindingContext context)
    {
      if (_loopContexts.Count == 0 ||
          !ReferenceEquals(_loopContexts[^1], context))
      {
        throw new InvalidOperationException("Loop binding contexts became unbalanced.");
      }

      _loopContexts.RemoveAt(_loopContexts.Count - 1);
    }

    private void RequireBoolCondition(
        BoundExpression condition,
        ExpressionSyntax syntax,
        string constructName)
    {
      if (condition.Type == TypeSymbol.Error ||
          condition.Type == TypeSymbol.Bool ||
          condition.Type == TypeSymbol.Never)
      {
        return;
      }

      Diagnostics.ReportConditionRequiresBool(
          GetExpressionSpan(syntax),
          constructName,
          condition.Type.Name);
    }

    private TypeSymbol UnifyIfBranchTypes(
        TypeSymbol thenType,
        TypeSymbol elseType,
        TextSpan elseSpan)
    {
      if (thenType == TypeSymbol.Error || elseType == TypeSymbol.Error)
        return TypeSymbol.Error;

      if (thenType == TypeSymbol.Never)
        return elseType;

      if (elseType == TypeSymbol.Never)
        return thenType;

      if (thenType == elseType)
        return thenType;

      Diagnostics.ReportIfBranchTypeMismatch(
          elseSpan,
          thenType.Name,
          elseType.Name);
      return TypeSymbol.Error;
    }

    private static string GetLabelName(SyntaxToken token)
    {
      if (token?.Value is string value)
        return value;

      var text = token?.Text ?? string.Empty;
      return text.Length > 0 && text[0] == '\''
          ? text.Substring(1)
          : text;
    }

    private BoundExpression BindAssignmentExpression(AssignmentExpressionSyntax syntax)
    {
      var expression = BindExpression(syntax.Expression);
      var targetSpan = GetExpressionSpan(syntax.Target);

      if (syntax.Target is not NameExpressionSyntax nameExpressionSyntax)
      {
        if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
        {
          Diagnostics.ReportInvalidAssignmentTarget(
              targetSpan,
              GetAssignmentTargetDisplayText(syntax.Target));
        }
        else
        {
          Diagnostics.ReportInvalidCompoundAssignmentTarget(targetSpan);
        }

        return BoundErrorExpression.Instance;
      }

      var name = nameExpressionSyntax.Name;

      VariableSymbol variable = LookupLocal(name);
      if (variable == null && _stateSymbols.TryGetValue(name, out var stateVariable))
        variable = stateVariable;

      if (variable == null)
      {
        var resolvedSymbol = ResolveVisibleSymbol(
            name,
            targetSpan,
            out var resolutionHadDiagnostic);
        if (resolutionHadDiagnostic)
          return BoundErrorExpression.Instance;

        if (resolvedSymbol != null)
        {
          if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
          {
            Diagnostics.ReportInvalidAssignmentTarget(
                targetSpan,
                name);
          }
          else
          {
            Diagnostics.ReportInvalidCompoundAssignmentTarget(
                targetSpan);
          }
        }
        else
        {
          Diagnostics.ReportUndefinedName(targetSpan, name);
        }

        return BoundErrorExpression.Instance;
      }

      if (!variable.IsMutable)
      {
        if (variable is StateVariableSymbol)
        {
          Diagnostics.ReportCannotAssignToImmutableState(
              targetSpan,
              name);
        }
        else
        {
          Diagnostics.ReportCannotAssignToImmutableLocal(
              targetSpan,
              name);
        }
      }

      if (expression.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;

      if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
      {
        if (!CanAssignToLocal(variable.Type, expression.Type))
        {
          Diagnostics.ReportTypeMismatch(
              GetExpressionSpan(syntax.Expression),
              variable.Type.Name,
              expression.Type.Name);
        }

        return new BoundAssignmentExpression(variable, expression);
      }

      var binarySyntaxKind = GetBinaryOperatorKindForCompoundAssignment(syntax.OperatorToken.Kind);
      if (binarySyntaxKind == null)
      {
        Diagnostics.ReportInvalidCompoundAssignmentTarget(targetSpan);
        return BoundErrorExpression.Instance;
      }

      var left = new BoundNameExpression(name, variable, variable.Type);
      var boundOperator = BindBinaryOperator(
          binarySyntaxKind.Value,
          variable.Type,
          expression.Type,
          GetExpressionSpan(syntax),
          reportDiagnostics: false);
      BoundExpression valueExpression;
      if (boundOperator != null)
      {
        valueExpression = new BoundBinaryExpression(left, boundOperator, expression);
      }
      else
      {
        valueExpression = BindUserDefinedOperatorCall(
            binarySyntaxKind.Value,
            left,
            expression,
            isUnary: false,
            GetExpressionSpan(syntax));
        if (valueExpression == null)
        {
          Diagnostics.ReportUnsupportedBinaryOperator(
              GetExpressionSpan(syntax),
              GetOperatorText(binarySyntaxKind.Value),
              variable.Type.Name,
              expression.Type.Name);
          return BoundErrorExpression.Instance;
        }
      }
      if (!CanAssignToLocal(variable.Type, valueExpression.Type))
      {
        Diagnostics.ReportTypeMismatch(
            GetExpressionSpan(syntax),
            variable.Type.Name,
            valueExpression.Type.Name);
      }

      return new BoundAssignmentExpression(variable, valueExpression);
    }

    private BoundExpression BindUnaryExpression(UnaryExpressionSyntax syntax)
    {
      var operand = BindExpression(syntax.Operand);
      if (operand.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;

      var span = GetExpressionSpan(syntax);

      switch (syntax.OperatorToken.Kind)
      {
        case SyntaxKind.PlusToken:
          if (IsNumericType(operand.Type))
            return operand;
          break;

        case SyntaxKind.MinusToken:
          if (IsNumericType(operand.Type))
          {
            var zeroLiteral = CreateZeroLiteral(operand.Type, span);
            var subtractionOperator = BindBinaryOperator(
                SyntaxKind.MinusToken,
                zeroLiteral.Type,
                operand.Type,
                span);
            if (subtractionOperator == null)
              return BoundErrorExpression.Instance;

            return new BoundBinaryExpression(
                zeroLiteral,
                subtractionOperator,
                operand);
          }
          break;

        case SyntaxKind.TildeToken:
          if (IsIntegerType(operand.Type))
          {
            var allBitsSetLiteral = CreateAllBitsSetLiteral(operand.Type, span);
            var xorOperator = BindBinaryOperator(
                SyntaxKind.CaretToken,
                operand.Type,
                allBitsSetLiteral.Type,
                span);
            if (xorOperator == null)
              return BoundErrorExpression.Instance;

            return new BoundBinaryExpression(
                operand,
                xorOperator,
                allBitsSetLiteral);
          }
          break;

        case SyntaxKind.BangToken when operand.Type == TypeSymbol.Bool:
          var builtInOperator = BindUnaryOperator(
              syntax.OperatorToken.Kind,
              operand.Type,
              span);
          if (builtInOperator == null)
            return BoundErrorExpression.Instance;
          return new BoundUnaryExpression(builtInOperator, operand);
      }

      var userOperator = BindUserDefinedOperatorCall(
          syntax.OperatorToken.Kind,
          operand,
          null,
          isUnary: true,
          span);
      if (userOperator != null)
        return userOperator;

      Diagnostics.ReportUnsupportedUnaryOperator(
          span,
          GetOperatorText(syntax.OperatorToken.Kind),
          operand.Type.Name);
      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindBinaryExpression(BinaryExpressionSyntax syntax)
    {
      var left = BindExpression(syntax.Left);
      var right = BindExpression(syntax.Right);

      if (left.Type == TypeSymbol.Error || right.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;

      var boundOperator = BindBinaryOperator(
          syntax.OperatorToken.Kind,
          left.Type,
          right.Type,
          GetExpressionSpan(syntax),
          reportDiagnostics: false);
      if (boundOperator == null)
      {
        var userOperator = BindUserDefinedOperatorCall(
            syntax.OperatorToken.Kind,
            left,
            right,
            isUnary: false,
            GetExpressionSpan(syntax));
        if (userOperator != null)
          return userOperator;

        Diagnostics.ReportUnsupportedBinaryOperator(
            GetExpressionSpan(syntax),
            GetOperatorText(syntax.OperatorToken.Kind),
            left.Type.Name,
            right.Type.Name);
        return BoundErrorExpression.Instance;
      }

      return new BoundBinaryExpression(left, boundOperator, right);
    }

    private BoundExpression BindUserDefinedOperatorCall(
        SyntaxKind operatorKind,
        BoundExpression left,
        BoundExpression right,
        bool isUnary,
        TextSpan span)
    {
      var name = (isUnary ? "@" : string.Empty) + GetOperatorText(operatorKind);
      if (!_methodGroupsByType.TryGetValue(left.Type, out var groups) ||
          !groups.TryGetValue(name, out var group))
      {
        return null;
      }

      var arguments = isUnary
          ? Array.Empty<BoundExpression>()
          : new[] { right };
      var applicable = new List<MethodSymbol>();
      var hasInaccessibleUserMethod = false;
      foreach (var method in group.Methods)
      {
        if (method is not UserMethodSymbol userMethod)
          continue;

        if (!IsUserMethodVisible(userMethod))
        {
          hasInaccessibleUserMethod = true;
          continue;
        }

        if (!userMethod.IsStatic &&
            method.Parameters.Count == arguments.Length &&
            IsApplicable(method, arguments))
        {
          applicable.Add(method);
        }
      }

      if (applicable.Count == 0)
      {
        if (hasInaccessibleUserMethod)
        {
          Diagnostics.ReportDeclarationNotPublic(span, group.Name);
        }
        else
        {
          Diagnostics.ReportNoApplicableMethodOverload(
              span,
              group.DisplayName,
              BuildArgumentTypeList(arguments));
        }
        return BoundErrorExpression.Instance;
      }

      var selected = SelectBestOverload(applicable, arguments, out var ambiguous);
      if (ambiguous || selected is not UserMethodSymbol selectedUserMethod)
      {
        Diagnostics.ReportAmbiguousMethodOverload(
            span,
            group.DisplayName,
            BuildMethodCandidateList(applicable));
        return BoundErrorExpression.Instance;
      }

      return new BoundUserFunctionCallExpression(
          selectedUserMethod.Function,
          arguments,
          left);
    }

    private BoundExpression BindStringLiteralExpression(StringLiteralExpressionSyntax syntax)
    {
      var value = syntax.StringToken.Value as string
          ?? UnquoteString(syntax.StringToken.Text ?? "");
      return new BoundLiteralExpression(value, TypeSymbol.String, syntax.StringToken.Span);
    }

    private BoundExpression BindIntegerLiteralExpression(IntegerLiteralExpressionSyntax syntax)
    {
      return syntax.LiteralToken.Kind switch
      {
        SyntaxKind.Int8Literal when syntax.LiteralToken.Value is sbyte int8Value =>
            new BoundLiteralExpression(int8Value, TypeSymbol.I8, syntax.LiteralToken.Span),
        SyntaxKind.UInt8Literal when syntax.LiteralToken.Value is byte uint8Value =>
            new BoundLiteralExpression(uint8Value, TypeSymbol.U8, syntax.LiteralToken.Span),
        SyntaxKind.Int16Literal when syntax.LiteralToken.Value is short int16Value =>
            new BoundLiteralExpression(int16Value, TypeSymbol.I16, syntax.LiteralToken.Span),
        SyntaxKind.UInt16Literal when syntax.LiteralToken.Value is ushort uint16Value =>
            new BoundLiteralExpression(uint16Value, TypeSymbol.U16, syntax.LiteralToken.Span),
        SyntaxKind.Int32Literal when syntax.LiteralToken.Value is int int32Value =>
            new BoundLiteralExpression(int32Value, TypeSymbol.I32, syntax.LiteralToken.Span),
        SyntaxKind.UInt32Literal when syntax.LiteralToken.Value is uint uint32Value =>
            new BoundLiteralExpression(uint32Value, TypeSymbol.U32, syntax.LiteralToken.Span),
        SyntaxKind.Int64Literal when syntax.LiteralToken.Value is long int64Value =>
            new BoundLiteralExpression(int64Value, TypeSymbol.I64, syntax.LiteralToken.Span),
        SyntaxKind.UInt64Literal when syntax.LiteralToken.Value is ulong uint64Value =>
            new BoundLiteralExpression(uint64Value, TypeSymbol.U64, syntax.LiteralToken.Span),
        _ => BoundErrorExpression.Instance
      };
    }

    private BoundExpression BindFloatLiteralExpression(FloatLiteralExpressionSyntax syntax)
    {
      return syntax.LiteralToken.Kind switch
      {
        SyntaxKind.Float32Literal when syntax.LiteralToken.Value is float floatValue =>
            new BoundLiteralExpression(floatValue, TypeSymbol.F32, syntax.LiteralToken.Span),
        SyntaxKind.Float64Literal when syntax.LiteralToken.Value is double doubleValue =>
            new BoundLiteralExpression(doubleValue, TypeSymbol.F64, syntax.LiteralToken.Span),
        _ => BoundErrorExpression.Instance
      };
    }

    private BoundExpression BindCharacterLiteralExpression(CharacterLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Value is char charValue)
        return new BoundLiteralExpression(charValue, TypeSymbol.Char, syntax.LiteralToken.Span);

      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindBooleanLiteralExpression(BooleanLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Value is bool boolValue)
        return new BoundLiteralExpression(boolValue, TypeSymbol.Bool, syntax.LiteralToken.Span);

      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindArrayLiteralExpression(ArrayLiteralExpressionSyntax syntax)
    {
      var elements = new List<BoundExpression>();

      foreach (var element in syntax.Elements)
        elements.Add(BindExpression(element));

      var elementType = InferArrayElementType(elements);
      if (elementType == null)
      {
        Diagnostics.ReportCannotInferArrayType(GetExpressionSpan(syntax));
        return BoundErrorExpression.Instance;
      }

      var hasError = false;
      for (var index = 0; index < elements.Count; index++)
      {
        var element = elements[index];
        if (element.Type == TypeSymbol.Error)
          continue;

        if (CanAssign(elementType, element.Type))
          continue;

        Diagnostics.ReportArrayElementTypeMismatch(
            GetExpressionSpan(syntax.Elements[index]),
            elementType.Name,
            element.Type.Name);
        hasError = true;
      }

      if (hasError)
        return BoundErrorExpression.Instance;

      return new BoundArrayLiteralExpression(elements, elementType);
    }

    private BoundExpression BindExternExpression(ExternExpressionSyntax syntax)
    {
      switch (syntax.Expression)
      {
        case CallExpressionSyntax call:
          return BindExternMethodCall(call);

        case MemberAccessExpressionSyntax member:
          return BindExternMemberAccess(member, ExternMemberKind.Getter, null);

        case AssignmentExpressionSyntax assignment
            when assignment.OperatorToken.Kind == SyntaxKind.EqualsToken &&
                 assignment.Target is MemberAccessExpressionSyntax setterMember:
          return BindExternMemberAccess(
              setterMember,
              ExternMemberKind.Setter,
              BindExpression(assignment.Expression));

        case NewExpressionSyntax constructor:
          return BindExternConstructor(constructor);

        case UnaryExpressionSyntax unary:
          return BindExternUnaryOperator(unary);

        case BinaryExpressionSyntax binary:
          return BindExternBinaryOperator(binary);

        default:
          Diagnostics.ReportUnsupportedExternalExpression(
              GetExpressionSpan(syntax.Expression));
          return BoundErrorExpression.Instance;
      }
    }

    private BoundExpression BindExternMethodCall(CallExpressionSyntax syntax)
    {
      if (syntax.Target is not MemberAccessExpressionSyntax member)
      {
        Diagnostics.ReportUnsupportedExternalExpression(GetExpressionSpan(syntax));
        return BoundErrorExpression.Instance;
      }

      var arguments = new List<BoundExpression>();
      foreach (var argumentSyntax in syntax.Arguments)
        arguments.Add(BindExpression(argumentSyntax));

      if (!TryBindExternalReceiver(
              member.Expression,
              out var containingType,
              out var receiver,
              out var isStatic))
      {
        return BoundErrorExpression.Instance;
      }

      if (!isStatic)
        arguments.Insert(0, receiver);

      var group = _environment.ExternCatalog.GetExternalMethodGroup(
          containingType,
          member.MemberName);
      return BindExternalMethodGroup(
          group,
          containingType,
          member.MemberName,
          arguments,
          isStatic,
          ExternMemberKind.Method,
          GetExpressionSpan(syntax));
    }

    private BoundExpression BindExternMemberAccess(
        MemberAccessExpressionSyntax syntax,
        ExternMemberKind memberKind,
        BoundExpression value)
    {
      if (!TryBindExternalReceiver(
              syntax.Expression,
              out var containingType,
              out var receiver,
              out var isStatic))
      {
        return BoundErrorExpression.Instance;
      }

      var arguments = new List<BoundExpression>();
      if (!isStatic)
        arguments.Add(receiver);
      if (value != null)
        arguments.Add(value);

      var group = _environment.ExternCatalog.GetExternalMethodGroup(
          containingType,
          syntax.MemberName);
      return BindExternalMethodGroup(
          group,
          containingType,
          syntax.MemberName,
          arguments,
          isStatic,
          memberKind,
          GetExpressionSpan(syntax));
    }

    private BoundExpression BindExternConstructor(NewExpressionSyntax syntax)
    {
      var type = BindTypeSyntax(syntax.Type);
      if (type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;

      var arguments = new List<BoundExpression>();
      foreach (var argumentSyntax in syntax.Arguments)
        arguments.Add(BindExpression(argumentSyntax));

      var group = _environment.ExternCatalog.GetExternalMethodGroup(type, "new");
      return BindExternalMethodGroup(
          group,
          type,
          "new",
          arguments,
          isStatic: true,
          ExternMemberKind.Constructor,
          GetExpressionSpan(syntax));
    }

    private BoundExpression BindExternUnaryOperator(UnaryExpressionSyntax syntax)
    {
      var operand = BindExpression(syntax.Operand);
      if (operand.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;

      var methodName = GetExternOperatorMethodName(syntax.OperatorToken.Kind, unary: true);
      if (methodName == null)
      {
        Diagnostics.ReportUnsupportedExternalExpression(GetExpressionSpan(syntax));
        return BoundErrorExpression.Instance;
      }

      var group = _environment.ExternCatalog.GetExternalMethodGroup(
          operand.Type,
          methodName);
      if (group != null)
      {
        return BindExternalMethodGroup(
            group,
            operand.Type,
            methodName,
            new[] { operand },
            isStatic: true,
            ExternMemberKind.Operator,
            GetExpressionSpan(syntax));
      }

      var builtIn = BindUnaryOperator(
          syntax.OperatorToken.Kind,
          operand.Type,
          GetExpressionSpan(syntax));
      return builtIn == null
          ? BoundErrorExpression.Instance
          : new BoundUnaryExpression(builtIn, operand);
    }

    private BoundExpression BindExternBinaryOperator(BinaryExpressionSyntax syntax)
    {
      var left = BindExpression(syntax.Left);
      var right = BindExpression(syntax.Right);
      if (left.Type == TypeSymbol.Error || right.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;

      var methodName = GetExternOperatorMethodName(syntax.OperatorToken.Kind, unary: false);
      if (methodName == null)
      {
        Diagnostics.ReportUnsupportedExternalExpression(GetExpressionSpan(syntax));
        return BoundErrorExpression.Instance;
      }

      var group = _environment.ExternCatalog.GetExternalMethodGroup(left.Type, methodName);
      if (group != null)
      {
        return BindExternalMethodGroup(
            group,
            left.Type,
            methodName,
            new[] { left, right },
            isStatic: true,
            ExternMemberKind.Operator,
            GetExpressionSpan(syntax));
      }

      var builtIn = BindBinaryOperator(
          syntax.OperatorToken.Kind,
          left.Type,
          right.Type,
          GetExpressionSpan(syntax));
      return builtIn == null
          ? BoundErrorExpression.Instance
          : new BoundBinaryExpression(left, builtIn, right);
    }

    private BoundExpression BindExternalMethodGroup(
        MethodGroupSymbol group,
        TypeSymbol containingType,
        string memberName,
        IReadOnlyList<BoundExpression> arguments,
        bool isStatic,
        ExternMemberKind memberKind,
        TextSpan span)
    {
      if (group == null)
      {
        Diagnostics.ReportUnknownExternalMember(
            span,
            containingType.RuntimeQualifiedName,
            memberName);
        return BoundErrorExpression.Instance;
      }

      var applicable = new List<MethodSymbol>();
      var matchingKindCount = 0;
      foreach (var method in group.Methods)
      {
        if (method is not ExternMethodSymbol externMethod ||
            externMethod.MemberKind != memberKind ||
            externMethod.IsStatic != isStatic)
        {
          continue;
        }

        matchingKindCount++;
        if (method.Parameters.Count == arguments.Count &&
            IsApplicable(method, arguments))
        {
          applicable.Add(method);
        }
      }

      if (applicable.Count == 0)
      {
        if (matchingKindCount > 0)
        {
          Diagnostics.ReportNoApplicableExternalOverload(
              span,
              group.DisplayName,
              BuildArgumentTypeList(arguments));
        }
        else if (group.RejectedCandidates.Count > 0)
        {
          Diagnostics.ReportExternalMemberNotExposed(
              span,
              group.DisplayName,
              BuildRejectedCandidateDetail(group.RejectedCandidates));
        }
        else
        {
          Diagnostics.ReportUnknownExternalMember(
              span,
              containingType.RuntimeQualifiedName,
              memberName);
        }

        return BoundErrorExpression.Instance;
      }

      var selected = SelectBestOverload(
          applicable,
          arguments,
          out var ambiguous);
      if (ambiguous || selected == null)
      {
        Diagnostics.ReportAmbiguousExternalOverload(
            span,
            group.DisplayName,
            BuildMethodCandidateList(applicable));
        return BoundErrorExpression.Instance;
      }

      var resultType = MapExternalResultType(selected.ReturnType);
      return new BoundCallExpression(
          new BoundNameExpression(group.DisplayName, group, TypeSymbol.MethodGroupPseudoType),
          arguments,
          selected,
          resultType);
    }

    private bool TryBindExternalReceiver(
        ExpressionSyntax syntax,
        out TypeSymbol containingType,
        out BoundExpression receiver,
        out bool isStatic)
    {
      if (TryResolveExternalTypeExpression(syntax, out containingType))
      {
        receiver = null;
        isStatic = true;
        return true;
      }

      receiver = BindExpression(syntax);
      if (receiver.Type == TypeSymbol.Error)
      {
        containingType = TypeSymbol.Error;
        isStatic = false;
        return false;
      }

      containingType = receiver.Type;
      isStatic = false;
      return true;
    }

    private bool TryResolveExternalTypeExpression(
        ExpressionSyntax syntax,
        out TypeSymbol type)
    {
      if (syntax is NameExpressionSyntax name)
      {
        if (name.Name == "Self" && _currentType != null)
        {
          type = _currentType;
          return true;
        }

        if (_declaredTypes.TryGetValue(name.Name, out type) ||
            BuiltInTypes.TryGetValue(name.Name, out type))
        {
          return true;
        }
      }

      if (TryGetQualifiedExpressionText(syntax, out var qualifiedName) &&
          _environment.ExternCatalog.TryGetTypeSymbol(qualifiedName, out type))
      {
        return true;
      }

      type = null;
      return false;
    }

    private static bool TryGetQualifiedExpressionText(
        ExpressionSyntax syntax,
        out string text)
    {
      if (syntax is NameExpressionSyntax name && name.QuestionToken == null)
      {
        text = name.Name;
        return true;
      }

      if (syntax is MemberAccessExpressionSyntax member &&
          member.QuestionToken == null &&
          TryGetQualifiedExpressionText(member.Expression, out var receiverText))
      {
        text = $"{receiverText}.{member.Name.Text}";
        return true;
      }

      text = null;
      return false;
    }

    private TypeSymbol MapExternalResultType(TypeSymbol runtimeType)
    {
      if (runtimeType == null)
        return TypeSymbol.Error;

      return _externalBindingsByRuntimeType.TryGetValue(
          runtimeType.RuntimeQualifiedName,
          out var binding) &&
          _declaredTypes.ContainsValue(binding)
          ? binding
          : runtimeType;
    }

    private static string GetExternOperatorMethodName(
        SyntaxKind kind,
        bool unary)
    {
      if (unary)
      {
        return kind switch
        {
          SyntaxKind.PlusToken => "op_UnaryPlus",
          SyntaxKind.MinusToken => "op_UnaryNegation",
          SyntaxKind.BangToken => "op_LogicalNot",
          SyntaxKind.TildeToken => "op_OnesComplement",
          _ => null
        };
      }

      return kind switch
      {
        SyntaxKind.PlusToken => "op_Addition",
        SyntaxKind.MinusToken => "op_Subtraction",
        SyntaxKind.StarToken => "op_Multiply",
        SyntaxKind.SlashToken => "op_Division",
        SyntaxKind.PercentToken => "op_Modulus",
        SyntaxKind.EqualsEqualsToken => "op_Equality",
        SyntaxKind.BangEqualsToken => "op_Inequality",
        SyntaxKind.LessToken => "op_LessThan",
        SyntaxKind.LessOrEqualsToken => "op_LessThanOrEqual",
        SyntaxKind.GreaterToken => "op_GreaterThan",
        SyntaxKind.GreaterOrEqualsToken => "op_GreaterThanOrEqual",
        SyntaxKind.AmpersandToken => "op_BitwiseAnd",
        SyntaxKind.PipeToken => "op_BitwiseOr",
        SyntaxKind.CaretToken => "op_ExclusiveOr",
        SyntaxKind.LessLessToken => "op_LeftShift",
        SyntaxKind.GreaterGreaterToken => "op_RightShift",
        _ => null
      };
    }

    private BoundExpression BindNameExpression(NameExpressionSyntax syntax)
    {
      var name = syntax.Name;
      var span = GetExpressionSpan(syntax);

      if (string.Equals(name, "self", StringComparison.Ordinal) &&
          _currentFunction?.IsMethod == true &&
          _currentFunction.IsStatic)
      {
        Diagnostics.ReportSelfUnavailableInStaticFunction(span);
        return BoundErrorExpression.Instance;
      }

      if (string.Equals(name, "Self", StringComparison.Ordinal))
      {
        if (_currentType != null)
          return new BoundNameExpression(name, _currentType, _currentType);

        Diagnostics.ReportSelfTypeOutsideImpl(span);
        return BoundErrorExpression.Instance;
      }

      var scopedSymbol = LookupScopedSymbol(name);
      if (scopedSymbol != null)
      {
        return new BoundNameExpression(
            name,
            scopedSymbol,
            GetExpressionType(scopedSymbol));
      }

      if ((_currentModule == null || _currentModule.IsEntry) &&
          _stateSymbols.TryGetValue(name, out var stateSymbol))
      {
        return new BoundNameExpression(
            name,
            stateSymbol,
            stateSymbol.Type);
      }

      if (_declaredTypes.TryGetValue(name, out var declaredType))
        return new BoundNameExpression(name, declaredType, declaredType);

      var hasFunction = _functionSymbols.TryGetValue(name, out var functionSymbol);
      var visibleSymbol = ResolveVisibleSymbol(
          name,
          span,
          out var resolutionHadDiagnostic);

      if (hasFunction && IsExternCallableSymbol(visibleSymbol))
      {
        Diagnostics.ReportAmbiguousUserFunctionExternCall(
            span,
            name,
            GetSymbolDisplayName(visibleSymbol));
        return BoundErrorExpression.Instance;
      }

      if (hasFunction)
      {
        if (functionSymbol.Parameters.Count == 0)
        {
          return new BoundUserFunctionCallExpression(
              functionSymbol,
              Array.Empty<BoundExpression>());
        }

        Diagnostics.ReportCallableRequiresArguments(
            span,
            functionSymbol.Name,
            functionSymbol.Parameters.Count);
        return BoundErrorExpression.Instance;
      }

      if (visibleSymbol == null)
      {
        if (resolutionHadDiagnostic)
          return new BoundNameExpression(name, null, TypeSymbol.Error);

        Diagnostics.ReportUndefinedName(span, name);
        return new BoundNameExpression(name, null, TypeSymbol.Error);
      }

      if (visibleSymbol is MethodGroupSymbol methodGroup)
        return BindImplicitMethodCall(syntax, methodGroup);

      return new BoundNameExpression(
          name,
          visibleSymbol,
          GetExpressionType(visibleSymbol));
    }

    private BoundExpression BindImplicitMethodCall(
        NameExpressionSyntax syntax,
        MethodGroupSymbol methodGroup)
    {
      var target = new BoundNameExpression(
          syntax.Name,
          methodGroup,
          GetExpressionType(methodGroup));

      if (methodGroup.Methods.Count > 0)
      {
        var hasZeroArgumentCandidate = false;
        foreach (var method in methodGroup.Methods)
        {
          if (method.Parameters.Count == 0)
          {
            hasZeroArgumentCandidate = true;
            break;
          }
        }

        if (!hasZeroArgumentCandidate)
        {
          Diagnostics.ReportCallableRequiresArguments(
              GetExpressionSpan(syntax),
              methodGroup.DisplayName,
              GetSharedParameterCount(methodGroup.Methods));
          return new BoundCallExpression(
              target,
              Array.Empty<BoundExpression>(),
              null,
              TypeSymbol.Error);
        }
      }

      var end = GetExpressionSpan(syntax).End;
      var openParen = new SyntaxToken(
          SyntaxKind.LeftParen,
          new TextSpan(end, 0),
          string.Empty);
      var closeParen = new SyntaxToken(
          SyntaxKind.RightParen,
          new TextSpan(end, 0),
          string.Empty);
      var implicitCall = new CallExpressionSyntax(
          syntax,
          openParen,
          Array.Empty<ExpressionSyntax>(),
          closeParen);
      return BindMethodCall(
          implicitCall,
          target,
          methodGroup,
          Array.Empty<BoundExpression>());
    }

    private BoundExpression BindImplicitUserMethodCall(
        MemberAccessExpressionSyntax syntax,
        BoundExpression receiver,
        MethodGroupSymbol methodGroup)
    {
      var target = new BoundMemberAccessExpression(
          receiver,
          syntax.MemberName,
          methodGroup,
          TypeSymbol.MethodGroupPseudoType);
      var end = syntax.QuestionToken?.Span.End ?? syntax.Name.Span.End;
      var implicitCall = new CallExpressionSyntax(
          syntax,
          new SyntaxToken(SyntaxKind.LeftParen, new TextSpan(end, 0), string.Empty),
          Array.Empty<ExpressionSyntax>(),
          new SyntaxToken(SyntaxKind.RightParen, new TextSpan(end, 0), string.Empty));
      return BindMethodCall(
          implicitCall,
          target,
          methodGroup,
          Array.Empty<BoundExpression>());
    }

    private BoundExpression BindMemberAccessExpression(MemberAccessExpressionSyntax syntax)
    {
      var receiver = BindExpression(syntax.Expression);
      var memberName = syntax.MemberName;

      if (receiver.Type == TypeSymbol.Error)
      {
        return new BoundMemberAccessExpression(
            receiver,
            memberName,
            null,
            TypeSymbol.Error);
      }

      var memberSymbol = LookupMember(receiver, memberName);
      if (memberSymbol == null)
      {
        Diagnostics.ReportUndefinedMember(
            syntax.Name.Span,
            GetReceiverDisplayName(receiver),
            memberName);
        return new BoundMemberAccessExpression(
            receiver,
            memberName,
            null,
            TypeSymbol.Error);
      }

      if (memberSymbol is MethodGroupSymbol methodGroup)
        return BindImplicitUserMethodCall(syntax, receiver, methodGroup);

      return new BoundMemberAccessExpression(
          receiver,
          memberName,
          memberSymbol,
          GetExpressionType(memberSymbol));
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax syntax)
    {
      var arguments = new List<BoundExpression>();

      foreach (var argument in syntax.Arguments)
        arguments.Add(BindExpression(argument));

      if (syntax.Target is NameExpressionSyntax nameExpression)
        return BindSimpleNameCall(syntax, nameExpression, arguments);

      if (syntax.Target is MemberAccessExpressionSyntax memberAccessSyntax)
      {
        var receiver = BindExpression(memberAccessSyntax.Expression);
        if (receiver.Type == TypeSymbol.Error)
          return BoundErrorExpression.Instance;

        var memberMethodGroup = LookupMember(receiver, memberAccessSyntax.MemberName)
            as MethodGroupSymbol;
        if (memberMethodGroup == null)
        {
          Diagnostics.ReportUndefinedMember(
              memberAccessSyntax.Name.Span,
              GetReceiverDisplayName(receiver),
              memberAccessSyntax.MemberName);
          return BoundErrorExpression.Instance;
        }

        var memberTarget = new BoundMemberAccessExpression(
            receiver,
            memberAccessSyntax.MemberName,
            memberMethodGroup,
            TypeSymbol.MethodGroupPseudoType);
        return BindMethodCall(syntax, memberTarget, memberMethodGroup, arguments);
      }

      var target = BindExpression(syntax.Target);

      if (target.Type == TypeSymbol.Error)
      {
        return new BoundCallExpression(
            target,
            arguments,
            null,
            TypeSymbol.Error);
      }

      if (GetReferencedSymbol(target) is MethodGroupSymbol methodGroup)
        return BindMethodCall(syntax, target, methodGroup, arguments);

      Diagnostics.ReportCallTargetIsNotMethod(
          GetExpressionSpan(syntax.Target),
          GetCallTargetDisplayName(target));
      return new BoundCallExpression(
          target,
          arguments,
          null,
          TypeSymbol.Error);
    }

    private BoundExpression BindSimpleNameCall(
        CallExpressionSyntax syntax,
        NameExpressionSyntax nameExpression,
        IReadOnlyList<BoundExpression> arguments)
    {
      var name = nameExpression.Name;
      var span = GetExpressionSpan(nameExpression);

      var scopedSymbol = LookupScopedSymbol(name);
      if (scopedSymbol != null)
      {
        var scopedTarget = new BoundNameExpression(
            name,
            scopedSymbol,
            GetExpressionType(scopedSymbol));
        Diagnostics.ReportCallTargetIsNotMethod(span, name);
        return new BoundCallExpression(
            scopedTarget,
            arguments,
            null,
            TypeSymbol.Error);
      }

      var hasFunction = _functionSymbols.TryGetValue(name, out var functionSymbol);
      var visibleSymbol = ResolveVisibleSymbol(
          name,
          span,
          out var resolutionHadDiagnostic);

      if (hasFunction && IsExternCallableSymbol(visibleSymbol))
      {
        Diagnostics.ReportAmbiguousUserFunctionExternCall(
            span,
            name,
            GetSymbolDisplayName(visibleSymbol));
        return new BoundCallExpression(
            new BoundNameExpression(name, visibleSymbol, GetExpressionType(visibleSymbol)),
            arguments,
            null,
            TypeSymbol.Error);
      }

      if (hasFunction)
        return BindUserFunctionCall(syntax, functionSymbol, arguments);

      if (visibleSymbol == null)
      {
        if (resolutionHadDiagnostic)
        {
          return new BoundCallExpression(
              new BoundNameExpression(name, null, TypeSymbol.Error),
              arguments,
              null,
              TypeSymbol.Error);
        }

        Diagnostics.ReportUndefinedName(span, name);
        return new BoundCallExpression(
            new BoundNameExpression(name, null, TypeSymbol.Error),
            arguments,
            null,
            TypeSymbol.Error);
      }

      var target = new BoundNameExpression(
          name,
          visibleSymbol,
          GetExpressionType(visibleSymbol));
      if (visibleSymbol is MethodGroupSymbol methodGroup)
        return BindMethodCall(syntax, target, methodGroup, arguments);

      Diagnostics.ReportCallTargetIsNotMethod(span, name);
      return new BoundCallExpression(
          target,
          arguments,
          null,
          TypeSymbol.Error);
    }

    private BoundExpression BindUserFunctionCall(
        CallExpressionSyntax syntax,
        FunctionSymbol functionSymbol,
        IReadOnlyList<BoundExpression> arguments)
    {
      if (ContainsError(arguments))
        return new BoundUserFunctionCallExpression(functionSymbol, arguments);

      if (functionSymbol.Parameters.Count != arguments.Count)
      {
        Diagnostics.ReportInvalidArgumentCount(
            GetExpressionSpan(syntax),
            functionSymbol.Name,
            functionSymbol.Parameters.Count,
            arguments.Count);
        return new BoundUserFunctionCallExpression(functionSymbol, arguments);
      }

      for (var index = 0; index < arguments.Count; index++)
      {
        if (TryGetCallConversionDistance(
                functionSymbol.Parameters[index].Type,
                arguments[index].Type,
                isExternalCall: false,
                out _))
        {
          continue;
        }

        Diagnostics.ReportTypeMismatch(
            GetExpressionSpan(syntax.Arguments[index]),
            functionSymbol.Parameters[index].Type.Name,
            arguments[index].Type.Name);
      }

      return new BoundUserFunctionCallExpression(functionSymbol, arguments);
    }

    private BoundExpression BindMethodCall(
        CallExpressionSyntax syntax,
        BoundExpression target,
        MethodGroupSymbol methodGroup,
        IReadOnlyList<BoundExpression> arguments)
    {
      if (ContainsError(arguments))
      {
        return new BoundCallExpression(
            target,
            arguments,
            null,
            TypeSymbol.Error);
      }

      var visibleMethods = new List<MethodSymbol>();
      var hasInaccessibleUserMethod = false;
      foreach (var method in methodGroup.Methods)
      {
        if (method is UserMethodSymbol candidateUserMethod &&
            !IsUserMethodVisible(candidateUserMethod))
        {
          hasInaccessibleUserMethod = true;
          continue;
        }

        visibleMethods.Add(method);
      }

      if (visibleMethods.Count == 0)
      {
        if (hasInaccessibleUserMethod)
        {
          Diagnostics.ReportDeclarationNotPublic(
              GetExpressionSpan(syntax),
              methodGroup.Name);
        }
        else if (methodGroup.RejectedCandidates.Count > 0)
        {
          Diagnostics.ReportExternCandidatesNotUdonCallable(
              GetExpressionSpan(syntax),
              methodGroup.DisplayName,
              BuildRejectedCandidateDetail(methodGroup.RejectedCandidates));
        }
        else
        {
          Diagnostics.ReportNoCallableExternCandidate(
              GetExpressionSpan(syntax),
              methodGroup.DisplayName);
        }

        return new BoundCallExpression(
            target,
            arguments,
            null,
            TypeSymbol.Error);
      }

      var sameArityMethods = new List<MethodSymbol>();
      var targetMemberAccess = target as BoundMemberAccessExpression;
      var targetReceiver = targetMemberAccess?.Receiver;
      var targetIsType = GetReferencedSymbol(targetReceiver) is TypeSymbol;
      foreach (var method in visibleMethods)
      {
        if (method.Parameters.Count == arguments.Count &&
            (targetMemberAccess == null || method.IsStatic == targetIsType))
        {
          sameArityMethods.Add(method);
        }
      }

      if (sameArityMethods.Count == 0)
      {
        var expectedCount = GetSharedParameterCount(visibleMethods);
        if (expectedCount >= 0)
        {
          Diagnostics.ReportInvalidArgumentCount(
              GetExpressionSpan(syntax),
              methodGroup.DisplayName,
              expectedCount,
              arguments.Count);
        }
        else
        {
          Diagnostics.ReportNoMatchingOverload(
              GetExpressionSpan(syntax),
              methodGroup.DisplayName,
              BuildArgumentTypeList(arguments));
        }

        return new BoundCallExpression(
            target,
            arguments,
            null,
            TypeSymbol.Error);
      }

      var applicableMethods = new List<MethodSymbol>();
      foreach (var method in sameArityMethods)
      {
        if (IsApplicable(method, arguments))
          applicableMethods.Add(method);
      }

      if (applicableMethods.Count == 0)
      {
        var hasUserMethod = false;
        foreach (var method in visibleMethods)
          hasUserMethod |= method is UserMethodSymbol;

        if (hasUserMethod)
        {
          Diagnostics.ReportNoApplicableMethodOverload(
              GetExpressionSpan(syntax),
              methodGroup.DisplayName,
              BuildArgumentTypeList(arguments));
        }
        else
        {
          Diagnostics.ReportNoMatchingOverload(
              GetExpressionSpan(syntax),
              methodGroup.DisplayName,
              BuildArgumentTypeList(arguments));
        }
        return new BoundCallExpression(
            target,
            arguments,
            null,
            TypeSymbol.Error);
      }

      var selectedMethod = SelectBestOverload(
          applicableMethods,
          arguments,
          out var overloadResolutionWasAmbiguous);
      if (overloadResolutionWasAmbiguous || selectedMethod == null)
      {
        var hasUserMethod = false;
        foreach (var method in applicableMethods)
          hasUserMethod |= method is UserMethodSymbol;

        if (hasUserMethod)
        {
          Diagnostics.ReportAmbiguousMethodOverload(
              GetExpressionSpan(syntax),
              methodGroup.DisplayName,
              BuildMethodCandidateList(applicableMethods));
        }
        else
        {
          Diagnostics.ReportAmbiguousExternOverload(
              GetExpressionSpan(syntax),
              methodGroup.DisplayName,
              BuildMethodCandidateList(applicableMethods));
        }
        return new BoundCallExpression(
            target,
            arguments,
            null,
            TypeSymbol.Error);
      }

      if (selectedMethod is UserMethodSymbol userMethod)
      {
        return new BoundUserFunctionCallExpression(
            userMethod.Function,
            arguments,
            userMethod.IsStatic ? null : targetReceiver);
      }

      return new BoundCallExpression(
          target,
          arguments,
          selectedMethod,
          selectedMethod.ReturnType);
    }

    private bool IsUserMethodVisible(UserMethodSymbol method)
    {
      return method.Function.IsPublic ||
             string.Equals(
                 method.Function.DeclaringModule ?? string.Empty,
                 _currentModule?.LogicalName ?? string.Empty,
                 StringComparison.Ordinal);
    }

    private BoundUnaryOperator BindUnaryOperator(
        SyntaxKind operatorKind,
        TypeSymbol operandType,
        TextSpan span)
    {
      switch (operatorKind)
      {
        case SyntaxKind.PlusToken when IsNumericType(operandType):
          return CreateUnaryOperator(
              BoundUnaryOperatorKind.Identity,
              operatorKind,
              operandType,
              operandType,
              "op_UnaryPlus",
              span);

        case SyntaxKind.MinusToken when IsNumericType(operandType):
          return CreateUnaryOperator(
              BoundUnaryOperatorKind.Negation,
              operatorKind,
              operandType,
              operandType,
              "op_UnaryNegation",
              span);

        case SyntaxKind.BangToken when operandType == TypeSymbol.Bool:
          return CreateUnaryOperator(
              BoundUnaryOperatorKind.LogicalNegation,
              operatorKind,
              operandType,
              TypeSymbol.Bool,
              "op_LogicalNot",
              span);

        case SyntaxKind.TildeToken when IsIntegerType(operandType):
          return CreateUnaryOperator(
              BoundUnaryOperatorKind.OnesComplement,
              operatorKind,
              operandType,
              operandType,
              "op_OnesComplement",
              span);
      }

      Diagnostics.ReportUnsupportedUnaryOperator(
          span,
          GetOperatorText(operatorKind),
          operandType.Name);
      return null;
    }

    private BoundBinaryOperator BindBinaryOperator(
        SyntaxKind operatorKind,
        TypeSymbol leftType,
        TypeSymbol rightType,
        TextSpan span,
        bool reportDiagnostics = true)
    {
      switch (operatorKind)
      {
        case SyntaxKind.AmpersandAmpersandToken:
          if (leftType != TypeSymbol.Bool || rightType != TypeSymbol.Bool)
          {
            Diagnostics.ReportShortCircuitRequiresBoolOperands(
                span,
                GetOperatorText(operatorKind),
                leftType.Name,
                rightType.Name);
            return null;
          }

          return new BoundBinaryOperator(
              BoundBinaryOperatorKind.LogicalAnd,
              operatorKind,
              leftType,
              rightType,
              TypeSymbol.Bool);

        case SyntaxKind.PipePipeToken:
          if (leftType != TypeSymbol.Bool || rightType != TypeSymbol.Bool)
          {
            Diagnostics.ReportShortCircuitRequiresBoolOperands(
                span,
                GetOperatorText(operatorKind),
                leftType.Name,
                rightType.Name);
            return null;
          }

          return new BoundBinaryOperator(
              BoundBinaryOperatorKind.LogicalOr,
              operatorKind,
              leftType,
              rightType,
              TypeSymbol.Bool);

        case SyntaxKind.PlusToken when leftType == rightType && IsNumericType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.Addition,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_Addition",
              span);

        case SyntaxKind.MinusToken when leftType == rightType && IsNumericType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.Subtraction,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_Subtraction",
              span);

        case SyntaxKind.StarToken when leftType == rightType && IsNumericType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.Multiplication,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_Multiply",
              span);

        case SyntaxKind.SlashToken when leftType == rightType && IsNumericType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.Division,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_Division",
              span);

        case SyntaxKind.PercentToken when leftType == rightType && IsNumericType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.Modulus,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_Modulus",
              span);

        case SyntaxKind.EqualsEqualsToken when leftType == rightType && IsEqualityPrimitiveType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.Equals,
              operatorKind,
              leftType,
              rightType,
              TypeSymbol.Bool,
              "op_Equality",
              span);

        case SyntaxKind.BangEqualsToken when leftType == rightType && IsEqualityPrimitiveType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.NotEquals,
              operatorKind,
              leftType,
              rightType,
              TypeSymbol.Bool,
              "op_Inequality",
              span);

        case SyntaxKind.LessToken when leftType == rightType && IsNumericType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.Less,
              operatorKind,
              leftType,
              rightType,
              TypeSymbol.Bool,
              "op_LessThan",
              span);

        case SyntaxKind.LessOrEqualsToken when leftType == rightType && IsNumericType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.LessOrEquals,
              operatorKind,
              leftType,
              rightType,
              TypeSymbol.Bool,
              "op_LessThanOrEqual",
              span);

        case SyntaxKind.GreaterToken when leftType == rightType && IsNumericType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.Greater,
              operatorKind,
              leftType,
              rightType,
              TypeSymbol.Bool,
              "op_GreaterThan",
              span);

        case SyntaxKind.GreaterOrEqualsToken when leftType == rightType && IsNumericType(leftType):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.GreaterOrEquals,
              operatorKind,
              leftType,
              rightType,
              TypeSymbol.Bool,
              "op_GreaterThanOrEqual",
              span);

        case SyntaxKind.AmpersandToken when leftType == rightType &&
            (IsIntegerType(leftType) || leftType == TypeSymbol.Bool):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.BitwiseAnd,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_BitwiseAnd",
              span);

        case SyntaxKind.PipeToken when leftType == rightType &&
            (IsIntegerType(leftType) || leftType == TypeSymbol.Bool):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.BitwiseOr,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_BitwiseOr",
              span);

        case SyntaxKind.CaretToken when leftType == rightType &&
            (IsIntegerType(leftType) || leftType == TypeSymbol.Bool):
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.BitwiseXor,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_ExclusiveOr",
              span);

        case SyntaxKind.LessLessToken when IsIntegerType(leftType) && rightType == TypeSymbol.I32:
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.LeftShift,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_LeftShift",
              span);

        case SyntaxKind.GreaterGreaterToken when IsIntegerType(leftType) && rightType == TypeSymbol.I32:
          return CreateBinaryOperator(
              BoundBinaryOperatorKind.RightShift,
              operatorKind,
              leftType,
              rightType,
              leftType,
              "op_RightShift",
              span);
      }

      if (reportDiagnostics)
      {
        Diagnostics.ReportUnsupportedBinaryOperator(
            span,
            GetOperatorText(operatorKind),
            leftType.Name,
            rightType.Name);
      }
      return null;
    }

    private BoundUnaryOperator CreateUnaryOperator(
        BoundUnaryOperatorKind kind,
        SyntaxKind operatorKind,
        TypeSymbol operandType,
        TypeSymbol resultType,
        string methodName,
        TextSpan span)
    {
      if (!TryResolveUnaryOperatorSignature(
              methodName,
              operatorKind,
              operandType,
              resultType,
              span,
              out var externSignature,
              out var wasAmbiguous))
      {
        if (!wasAmbiguous)
        {
          Diagnostics.ReportUnsupportedUnaryOperator(
              span,
              GetOperatorText(operatorKind),
              operandType.Name);
        }

        return null;
      }

      return new BoundUnaryOperator(
          kind,
          operatorKind,
          operandType,
          resultType,
          externSignature);
    }

    private BoundBinaryOperator CreateBinaryOperator(
        BoundBinaryOperatorKind kind,
        SyntaxKind operatorKind,
        TypeSymbol leftType,
        TypeSymbol rightType,
        TypeSymbol resultType,
        string methodName,
        TextSpan span)
    {
      if (!TryResolveBinaryOperatorSignature(
              methodName,
              operatorKind,
              leftType,
              rightType,
              resultType,
              span,
              out var externSignature,
              out var wasAmbiguous))
      {
        if (!wasAmbiguous)
        {
          Diagnostics.ReportUnsupportedBinaryOperator(
              span,
              GetOperatorText(operatorKind),
              leftType.Name,
              rightType.Name);
        }

        return null;
      }

      return new BoundBinaryOperator(
          kind,
          operatorKind,
          leftType,
          rightType,
          resultType,
          externSignature);
    }

    private bool TryResolveUnaryOperatorSignature(
        string methodName,
        SyntaxKind operatorKind,
        TypeSymbol operandType,
        TypeSymbol resultType,
        TextSpan span,
        out string externSignature,
        out bool wasAmbiguous)
    {
      var candidates = _environment.ExternCatalog.GetUnaryOperatorSignatures(
          methodName,
          operandType,
          resultType);
      return TryResolveOperatorSignature(
          candidates,
          operatorKind,
          span,
          operandType.Name,
          out externSignature,
          out wasAmbiguous);
    }

    private bool TryResolveBinaryOperatorSignature(
        string methodName,
        SyntaxKind operatorKind,
        TypeSymbol leftType,
        TypeSymbol rightType,
        TypeSymbol resultType,
        TextSpan span,
        out string externSignature,
        out bool wasAmbiguous)
    {
      var candidates = _environment.ExternCatalog.GetBinaryOperatorSignatures(
          methodName,
          leftType,
          rightType,
          resultType);
      return TryResolveOperatorSignature(
          candidates,
          operatorKind,
          span,
          $"{leftType.Name}, {rightType.Name}",
          out externSignature,
          out wasAmbiguous);
    }

    private bool TryResolveOperatorSignature(
        IReadOnlyList<string> candidates,
        SyntaxKind operatorKind,
        TextSpan span,
        string operandTypes,
        out string externSignature,
        out bool wasAmbiguous)
    {
      externSignature = null;
      wasAmbiguous = false;

      if (candidates.Count == 1)
      {
        externSignature = candidates[0];
        return true;
      }

      if (candidates.Count > 1)
      {
        wasAmbiguous = true;
        Diagnostics.ReportAmbiguousOperator(
            span,
            GetOperatorText(operatorKind),
            operandTypes,
            string.Join(", ", candidates));
        return false;
      }

      return false;
    }

    private static SyntaxKind? GetBinaryOperatorKindForCompoundAssignment(SyntaxKind operatorKind)
    {
      return operatorKind switch
      {
        SyntaxKind.PlusEqualsToken => SyntaxKind.PlusToken,
        SyntaxKind.MinusEqualsToken => SyntaxKind.MinusToken,
        SyntaxKind.StarEqualsToken => SyntaxKind.StarToken,
        SyntaxKind.SlashEqualsToken => SyntaxKind.SlashToken,
        SyntaxKind.PercentEqualsToken => SyntaxKind.PercentToken,
        SyntaxKind.AmpersandEqualsToken => SyntaxKind.AmpersandToken,
        SyntaxKind.PipeEqualsToken => SyntaxKind.PipeToken,
        SyntaxKind.CaretEqualsToken => SyntaxKind.CaretToken,
        SyntaxKind.LessLessEqualsToken => SyntaxKind.LessLessToken,
        SyntaxKind.GreaterGreaterEqualsToken => SyntaxKind.GreaterGreaterToken,
        _ => null
      };
    }

    private static BoundLiteralExpression CreateZeroLiteral(
        TypeSymbol type,
        TextSpan span)
    {
      if (type == TypeSymbol.I8)
        return new BoundLiteralExpression((sbyte)0, TypeSymbol.I8, span);

      if (type == TypeSymbol.U8)
        return new BoundLiteralExpression((byte)0, TypeSymbol.U8, span);

      if (type == TypeSymbol.I16)
        return new BoundLiteralExpression((short)0, TypeSymbol.I16, span);

      if (type == TypeSymbol.U16)
        return new BoundLiteralExpression((ushort)0, TypeSymbol.U16, span);

      if (type == TypeSymbol.I32)
        return new BoundLiteralExpression(0, TypeSymbol.I32, span);

      if (type == TypeSymbol.U32)
        return new BoundLiteralExpression((uint)0, TypeSymbol.U32, span);

      if (type == TypeSymbol.I64)
        return new BoundLiteralExpression(0L, TypeSymbol.I64, span);

      if (type == TypeSymbol.U64)
        return new BoundLiteralExpression(0UL, TypeSymbol.U64, span);

      if (type == TypeSymbol.F32)
        return new BoundLiteralExpression(0f, TypeSymbol.F32, span);

      if (type == TypeSymbol.F64)
        return new BoundLiteralExpression(0d, TypeSymbol.F64, span);

      throw new InvalidOperationException(
          $"Cannot create zero literal for type '{type.Name}'.");
    }

    private static BoundLiteralExpression CreateAllBitsSetLiteral(
        TypeSymbol type,
        TextSpan span)
    {
      if (type == TypeSymbol.I8)
        return new BoundLiteralExpression((sbyte)-1, TypeSymbol.I8, span);

      if (type == TypeSymbol.U8)
        return new BoundLiteralExpression(byte.MaxValue, TypeSymbol.U8, span);

      if (type == TypeSymbol.I16)
        return new BoundLiteralExpression((short)-1, TypeSymbol.I16, span);

      if (type == TypeSymbol.U16)
        return new BoundLiteralExpression(ushort.MaxValue, TypeSymbol.U16, span);

      if (type == TypeSymbol.I32)
        return new BoundLiteralExpression(-1, TypeSymbol.I32, span);

      if (type == TypeSymbol.U32)
        return new BoundLiteralExpression(uint.MaxValue, TypeSymbol.U32, span);

      if (type == TypeSymbol.I64)
        return new BoundLiteralExpression(-1L, TypeSymbol.I64, span);

      if (type == TypeSymbol.U64)
        return new BoundLiteralExpression(ulong.MaxValue, TypeSymbol.U64, span);

      throw new InvalidOperationException(
          $"Cannot create all-bits-set literal for type '{type.Name}'.");
    }

    private static bool IsNumericType(TypeSymbol type)
    {
      return TryGetNumericCategoryAndRank(type, out _, out _);
    }

    private static bool IsIntegerType(TypeSymbol type)
    {
      return type.TypeKind is TypeKind.I8 or
          TypeKind.U8 or
          TypeKind.I16 or
          TypeKind.U16 or
          TypeKind.I32 or
          TypeKind.U32 or
          TypeKind.I64 or
          TypeKind.U64;
    }

    private static bool IsEqualityPrimitiveType(TypeSymbol type)
    {
      return type == TypeSymbol.Bool ||
          type == TypeSymbol.Char ||
          type == TypeSymbol.String ||
          IsNumericType(type);
    }

    private static string GetOperatorText(SyntaxKind kind)
    {
      return kind switch
      {
        SyntaxKind.PlusToken => "+",
        SyntaxKind.MinusToken => "-",
        SyntaxKind.StarToken => "*",
        SyntaxKind.SlashToken => "/",
        SyntaxKind.PercentToken => "%",
        SyntaxKind.EqualsEqualsToken => "==",
        SyntaxKind.BangEqualsToken => "!=",
        SyntaxKind.LessToken => "<",
        SyntaxKind.LessOrEqualsToken => "<=",
        SyntaxKind.GreaterToken => ">",
        SyntaxKind.GreaterOrEqualsToken => ">=",
        SyntaxKind.BangToken => "!",
        SyntaxKind.AmpersandAmpersandToken => "&&",
        SyntaxKind.PipePipeToken => "||",
        SyntaxKind.TildeToken => "~",
        SyntaxKind.AmpersandToken => "&",
        SyntaxKind.PipeToken => "|",
        SyntaxKind.CaretToken => "^",
        SyntaxKind.LessLessToken => "<<",
        SyntaxKind.GreaterGreaterToken => ">>",
        SyntaxKind.EqualsToken => "=",
        SyntaxKind.PlusEqualsToken => "+=",
        SyntaxKind.MinusEqualsToken => "-=",
        SyntaxKind.StarEqualsToken => "*=",
        SyntaxKind.SlashEqualsToken => "/=",
        SyntaxKind.PercentEqualsToken => "%=",
        SyntaxKind.AmpersandEqualsToken => "&=",
        SyntaxKind.PipeEqualsToken => "|=",
        SyntaxKind.CaretEqualsToken => "^=",
        SyntaxKind.LessLessEqualsToken => "<<=",
        SyntaxKind.GreaterGreaterEqualsToken => ">>=",
        _ => kind.ToString()
      };
    }

    private static string GetAssignmentTargetDisplayText(ExpressionSyntax syntax)
    {
      if (syntax is NameExpressionSyntax nameExpression)
        return nameExpression.Name;

      if (syntax is MemberAccessExpressionSyntax memberAccessExpression)
        return memberAccessExpression.Name.Text ?? "<member>";

      return syntax.GetType().Name;
    }

    private Symbol LookupMember(BoundExpression receiver, string memberName)
    {
      var receiverSymbol = GetReferencedSymbol(receiver);
      if (receiverSymbol is TypeSymbol explicitTypeSymbol)
      {
        if (_methodGroupsByType.TryGetValue(explicitTypeSymbol, out var typeGroups) &&
            typeGroups.TryGetValue(memberName, out var explicitMethodGroup))
        {
          return explicitMethodGroup;
        }
      }

      if (_methodGroupsByType.TryGetValue(receiver.Type, out var groups) &&
          groups.TryGetValue(memberName, out var methods))
      {
        return methods;
      }

      return null;
    }

    private LocalVariableSymbol LookupLocal(string name)
    {
      return _scope != null && _scope.TryLookupLocal(name, out var local)
          ? local
          : null;
    }

    private Symbol LookupScopedSymbol(string name)
    {
      return _scope != null && _scope.TryLookupSymbol(name, out var symbol)
          ? symbol
          : null;
    }

    private Symbol ResolveVisibleSymbol(string name, TextSpan span)
    {
      return ResolveVisibleSymbol(name, span, out _);
    }

    private Symbol ResolveVisibleSymbol(
        string name,
        TextSpan span,
        out bool resolutionHadDiagnostic)
    {
      resolutionHadDiagnostic = false;

      return _declaredTypes.TryGetValue(name, out var declaredType)
          ? declaredType
          : null;
    }

    private static bool IsExternCallableSymbol(Symbol symbol)
    {
      return symbol is MethodGroupSymbol || symbol is MethodSymbol;
    }

    private static string GetSymbolDisplayName(Symbol symbol)
    {
      if (symbol is NamespaceSymbol namespaceSymbol)
        return namespaceSymbol.QualifiedName;

      if (symbol is TypeSymbol typeSymbol)
        return typeSymbol.QualifiedName;

      if (symbol is MethodGroupSymbol methodGroup)
        return methodGroup.DisplayName;

      if (symbol is MethodSymbol method)
        return method.DisplayName;

      return symbol?.Name ?? "<unknown>";
    }

    private static Symbol GetReferencedSymbol(BoundExpression expression)
    {
      if (expression is BoundNameExpression nameExpression)
        return nameExpression.Symbol;

      if (expression is BoundMemberAccessExpression memberAccessExpression)
        return memberAccessExpression.MemberSymbol;

      return null;
    }

    private static TypeSymbol GetExpressionType(Symbol symbol)
    {
      if (symbol is TypeSymbol typeSymbol)
        return typeSymbol;

      if (symbol is NamespaceSymbol)
        return TypeSymbol.NamespacePseudoType;

      if (symbol is ParameterSymbol parameterSymbol)
        return parameterSymbol.Type;

      if (symbol is VariableSymbol variableSymbol)
        return variableSymbol.Type;

      if (symbol is MethodGroupSymbol || symbol is MethodSymbol)
        return TypeSymbol.MethodGroupPseudoType;

      if (symbol is FunctionSymbol)
        return TypeSymbol.MethodGroupPseudoType;

      return TypeSymbol.Error;
    }

    private static string GetReceiverDisplayName(BoundExpression receiver)
    {
      var symbol = GetReferencedSymbol(receiver);
      if (symbol is NamespaceSymbol namespaceSymbol)
        return namespaceSymbol.Name;

      if (symbol is TypeSymbol typeSymbol)
        return typeSymbol.Name;

      return receiver.Type.Name;
    }

    private static string GetCallTargetDisplayName(BoundExpression target)
    {
      var symbol = GetReferencedSymbol(target);
      if (symbol is MethodSymbol methodSymbol)
        return methodSymbol.DisplayName;

      if (symbol != null)
        return symbol.Name;

      return target.Type.Name;
    }

    private static bool ContainsError(IReadOnlyList<BoundExpression> arguments)
    {
      foreach (var argument in arguments)
      {
        if (argument.Type == TypeSymbol.Error)
          return true;
      }

      return false;
    }

    private static int GetSharedParameterCount(IReadOnlyList<MethodSymbol> methods)
    {
      if (methods.Count == 0)
        return -1;

      var count = methods[0].Parameters.Count;
      for (var index = 1; index < methods.Count; index++)
      {
        if (methods[index].Parameters.Count != count)
          return -1;
      }

      return count;
    }

    private static bool IsApplicable(
        MethodSymbol method,
        IReadOnlyList<BoundExpression> arguments)
    {
      for (var index = 0; index < arguments.Count; index++)
      {
        if (!TryGetCallConversionDistance(
                method.Parameters[index].Type,
                arguments[index].Type,
                method is ExternMethodSymbol,
                out _))
          return false;
      }

      return true;
    }

    private static MethodSymbol SelectBestOverload(
        IReadOnlyList<MethodSymbol> methods,
        IReadOnlyList<BoundExpression> arguments,
        out bool overloadResolutionWasAmbiguous)
    {
      overloadResolutionWasAmbiguous = false;
      MethodSymbol bestMethod = null;
      var bestDistance = int.MaxValue;

      foreach (var method in methods)
      {
        if (!TryGetTotalCallDistance(method, arguments, out var totalDistance))
          continue;

        if (bestMethod == null || totalDistance < bestDistance)
        {
          bestMethod = method;
          bestDistance = totalDistance;
          overloadResolutionWasAmbiguous = false;
          continue;
        }

        if (totalDistance == bestDistance)
          overloadResolutionWasAmbiguous = true;
      }

      return bestMethod;
    }

    private static bool TryGetTotalCallDistance(
        MethodSymbol method,
        IReadOnlyList<BoundExpression> arguments,
        out int totalDistance)
    {
      totalDistance = 0;

      for (var index = 0; index < arguments.Count; index++)
      {
        if (!TryGetCallConversionDistance(
                method.Parameters[index].Type,
                arguments[index].Type,
                method is ExternMethodSymbol,
                out var distance))
        {
          totalDistance = 0;
          return false;
        }

        totalDistance += distance;
      }

      return true;
    }

    private static bool TryGetCallConversionDistance(
        TypeSymbol targetType,
        TypeSymbol sourceType,
        bool isExternalCall,
        out int distance)
    {
      if (TryGetConversionDistance(targetType, sourceType, out distance))
        return true;

      if (isExternalCall &&
          !string.IsNullOrEmpty(targetType.RuntimeQualifiedName) &&
          string.Equals(
              targetType.RuntimeQualifiedName,
              sourceType.RuntimeQualifiedName,
              StringComparison.Ordinal))
      {
        distance = 0;
        return true;
      }

      if (isExternalCall &&
          targetType == TypeSymbol.Object &&
          sourceType != TypeSymbol.Error &&
          sourceType != TypeSymbol.Void)
      {
        distance = 1000;
        return true;
      }

      distance = 0;
      return false;
    }

    private static string BuildMethodCandidateList(IReadOnlyList<MethodSymbol> methods)
    {
      var candidates = new string[methods.Count];
      for (var index = 0; index < methods.Count; index++)
        candidates[index] = BuildMethodSignature(methods[index]);

      return string.Join(", ", candidates);
    }

    private static string BuildMethodSignature(MethodSymbol method)
    {
      var parameterTypes = new string[method.Parameters.Count];
      for (var index = 0; index < method.Parameters.Count; index++)
        parameterTypes[index] = method.Parameters[index].Type.Name;

      return $"{method.DisplayName}({string.Join(", ", parameterTypes)})";
    }

    private static string BuildRejectedCandidateDetail(IReadOnlyList<ExternCandidate> candidates)
    {
      if (candidates.Count == 0)
        return string.Empty;

      var maxCount = candidates.Count < 3 ? candidates.Count : 3;
      var details = new string[maxCount];
      for (var index = 0; index < maxCount; index++)
      {
        var candidate = candidates[index];
        details[index] = $"{candidate.DisplayName}: {candidate.RejectionReason}";
      }

      var detailText = string.Join("; ", details);
      if (candidates.Count > maxCount)
        detailText += $" (+{candidates.Count - maxCount} more)";

      return detailText;
    }

    private static string BuildArgumentTypeList(IReadOnlyList<BoundExpression> arguments)
    {
      if (arguments.Count == 0)
        return "(none)";

      var names = new string[arguments.Count];
      for (var index = 0; index < arguments.Count; index++)
        names[index] = arguments[index].Type.Name;

      return string.Join(", ", names);
    }

    private static TypeSymbol InferArrayElementType(IReadOnlyList<BoundExpression> elements)
    {
      TypeSymbol inferredType = null;

      foreach (var element in elements)
      {
        if (element.Type == TypeSymbol.Error || element.Type == TypeSymbol.Null)
          continue;

        if (inferredType == null)
        {
          inferredType = element.Type;
          continue;
        }

        if (TryGetCommonElementType(inferredType, element.Type, out var commonType))
          inferredType = commonType;
      }

      return inferredType;
    }

    private static bool TryGetCommonElementType(
        TypeSymbol left,
        TypeSymbol right,
        out TypeSymbol commonType)
    {
      if (left == right)
      {
        commonType = left;
        return true;
      }

      if (CanAssign(left, right))
      {
        commonType = left;
        return true;
      }

      if (CanAssign(right, left))
      {
        commonType = right;
        return true;
      }

      commonType = null;
      return false;
    }

    private static bool CanAssign(TypeSymbol targetType, TypeSymbol sourceType)
    {
      return TryGetConversionDistance(targetType, sourceType, out _);
    }

    private static bool CanAssignToLocal(TypeSymbol targetType, TypeSymbol sourceType)
    {
      if (targetType == TypeSymbol.Error || sourceType == TypeSymbol.Error)
        return true;

      if (sourceType == TypeSymbol.Never)
        return true;

      if (targetType == sourceType)
        return true;

      return sourceType == TypeSymbol.Null && targetType.IsReferenceType;
    }

    private static bool TryGetConversionDistance(
        TypeSymbol targetType,
        TypeSymbol sourceType,
        out int distance)
    {
      if (targetType == TypeSymbol.Error || sourceType == TypeSymbol.Error)
      {
        distance = 0;
        return true;
      }

      if (sourceType == TypeSymbol.Never)
      {
        distance = 0;
        return true;
      }

      if (targetType == sourceType)
      {
        distance = 0;
        return true;
      }

      if (sourceType == TypeSymbol.Null && targetType.IsReferenceType)
      {
        distance = 0;
        return true;
      }

      return TryGetNumericWideningDistance(targetType, sourceType, out distance);
    }

    private static bool TryGetNumericWideningDistance(
        TypeSymbol targetType,
        TypeSymbol sourceType,
        out int distance)
    {
      distance = 0;

      if (!TryGetNumericCategoryAndRank(targetType, out var targetCategory, out var targetRank) ||
          !TryGetNumericCategoryAndRank(sourceType, out var sourceCategory, out var sourceRank))
      {
        return false;
      }

      if (targetCategory != sourceCategory || sourceRank > targetRank)
        return false;

      distance = targetRank - sourceRank;
      return true;
    }

    private static bool TryGetNumericCategoryAndRank(
        TypeSymbol type,
        out NumericCategory category,
        out int rank)
    {
      switch (type.TypeKind)
      {
        case TypeKind.I8:
          category = NumericCategory.SignedInteger;
          rank = 0;
          return true;

        case TypeKind.I16:
          category = NumericCategory.SignedInteger;
          rank = 1;
          return true;

        case TypeKind.I32:
          category = NumericCategory.SignedInteger;
          rank = 2;
          return true;

        case TypeKind.I64:
          category = NumericCategory.SignedInteger;
          rank = 3;
          return true;

        case TypeKind.U8:
          category = NumericCategory.UnsignedInteger;
          rank = 0;
          return true;

        case TypeKind.U16:
          category = NumericCategory.UnsignedInteger;
          rank = 1;
          return true;

        case TypeKind.U32:
          category = NumericCategory.UnsignedInteger;
          rank = 2;
          return true;

        case TypeKind.U64:
          category = NumericCategory.UnsignedInteger;
          rank = 3;
          return true;

        case TypeKind.F32:
          category = NumericCategory.FloatingPoint;
          rank = 0;
          return true;

        case TypeKind.F64:
          category = NumericCategory.FloatingPoint;
          rank = 1;
          return true;

        default:
          category = default;
          rank = -1;
          return false;
      }
    }

    private static TextSpan GetUseDirectiveSpan(UseDirectiveSyntax syntax)
    {
      var end = syntax.SemicolonToken?.Span.End ?? syntax.UseKeyword.Span.End;

      if (end <= syntax.UseKeyword.Span.Start)
      {
        if (syntax.Alias != null)
          end = syntax.Alias.Span.End;
        else if (syntax.Path != null && syntax.Path.Identifiers.Count > 0)
          end = syntax.Path.Identifiers[^1].Span.End;
      }

      return TextSpan.FromBounds(syntax.UseKeyword.Span.Start, end);
    }

    private static TextSpan GetMemberSpan(MemberSyntax member)
    {
      if (member is StateDeclarationSyntax state)
      {
        var start = state.PubKeyword?.Span.Start ??
            state.SynchronizationModifier?.SyncKeyword.Span.Start ??
            state.LetKeyword.Span.Start;
        return TextSpan.FromBounds(start, state.SemicolonToken.Span.End);
      }

      if (member is EventDeclarationSyntax eventDeclaration)
      {
        return TextSpan.FromBounds(
            eventDeclaration.OnKeyword.Span.Start,
            eventDeclaration.Body.CloseBraceToken.Span.End);
      }

      return new TextSpan(0, 0);
    }

    private static TextSpan GetFunctionNameSpan(FunctionDeclarationSyntax syntax)
    {
      if (syntax.OperatorToken != null)
      {
        return TextSpan.FromBounds(
            syntax.AtToken?.Span.Start ?? syntax.OperatorToken.Span.Start,
            syntax.OperatorToken.Span.End);
      }

      return syntax.QuestionToken == null
          ? syntax.Identifier.Span
          : TextSpan.FromBounds(
              syntax.Identifier.Span.Start,
              syntax.QuestionToken.Span.End);
    }

    private static TextSpan GetStatementSpan(StatementSyntax syntax)
    {
      if (syntax is ExpressionStatementSyntax expressionStatement)
      {
        var expressionSpan = GetExpressionSpan(expressionStatement.Expression);
        return TextSpan.FromBounds(
            expressionSpan.Start,
            expressionStatement.SemicolonToken?.Span.End ?? expressionSpan.End);
      }

      if (syntax is VariableDeclarationStatementSyntax variableDeclarationStatement)
      {
        return TextSpan.FromBounds(
            variableDeclarationStatement.LetKeyword.Span.Start,
            variableDeclarationStatement.SemicolonToken.Span.End);
      }

      if (syntax is ReturnStatementSyntax returnStatement)
      {
        return TextSpan.FromBounds(
            returnStatement.ReturnKeyword.Span.Start,
            returnStatement.SemicolonToken.Span.End);
      }

      if (syntax is BreakStatementSyntax breakStatement)
      {
        return TextSpan.FromBounds(
            breakStatement.BreakKeyword.Span.Start,
            breakStatement.SemicolonToken.Span.End);
      }

      if (syntax is ContinueStatementSyntax continueStatement)
      {
        return TextSpan.FromBounds(
            continueStatement.ContinueKeyword.Span.Start,
            continueStatement.SemicolonToken.Span.End);
      }

      if (syntax is RedoStatementSyntax redoStatement)
      {
        return TextSpan.FromBounds(
            redoStatement.RedoKeyword.Span.Start,
            redoStatement.SemicolonToken.Span.End);
      }

      if (syntax is BlockStatementSyntax blockStatement)
      {
        return TextSpan.FromBounds(
            blockStatement.OpenBraceToken.Span.Start,
            blockStatement.CloseBraceToken.Span.End);
      }

      return new TextSpan(0, 0);
    }

    private static TextSpan GetExpressionSpan(ExpressionSyntax syntax)
    {
      if (syntax is ExternExpressionSyntax externExpression)
      {
        return TextSpan.FromBounds(
            externExpression.ExternKeyword.Span.Start,
            GetExpressionSpan(externExpression.Expression).End);
      }

      if (syntax is NewExpressionSyntax newExpression)
      {
        return TextSpan.FromBounds(
            newExpression.NewKeyword.Span.Start,
            newExpression.CloseParenToken.Span.End);
      }

      if (syntax is AssignmentExpressionSyntax assignmentExpression)
      {
        var expressionSpan = GetExpressionSpan(assignmentExpression.Expression);
        return TextSpan.FromBounds(
            GetExpressionSpan(assignmentExpression.Target).Start,
            expressionSpan.End);
      }

      if (syntax is ParenthesizedExpressionSyntax parenthesizedExpression)
      {
        return TextSpan.FromBounds(
            parenthesizedExpression.OpenParenToken.Span.Start,
            parenthesizedExpression.CloseParenToken.Span.End);
      }

      if (syntax is UnaryExpressionSyntax unaryExpression)
      {
        var operandSpan = GetExpressionSpan(unaryExpression.Operand);
        return TextSpan.FromBounds(
            unaryExpression.OperatorToken.Span.Start,
            operandSpan.End);
      }

      if (syntax is BinaryExpressionSyntax binaryExpression)
      {
        var leftSpan = GetExpressionSpan(binaryExpression.Left);
        var rightSpan = GetExpressionSpan(binaryExpression.Right);
        return TextSpan.FromBounds(leftSpan.Start, rightSpan.End);
      }

      if (syntax is IfExpressionSyntax ifExpression)
      {
        var end = ifExpression.ElseExpression == null
            ? ifExpression.ThenBlock.CloseBraceToken.Span.End
            : GetExpressionSpan(ifExpression.ElseExpression).End;
        return TextSpan.FromBounds(ifExpression.IfKeyword.Span.Start, end);
      }

      if (syntax is BlockExpressionSyntax blockExpression)
      {
        return TextSpan.FromBounds(
            blockExpression.Block.OpenBraceToken.Span.Start,
            blockExpression.Block.CloseBraceToken.Span.End);
      }

      if (syntax is WhileExpressionSyntax whileExpression)
      {
        var start = whileExpression.Label?.LabelToken.Span.Start ??
            whileExpression.WhileKeyword.Span.Start;
        return TextSpan.FromBounds(
            start,
            whileExpression.Body.CloseBraceToken.Span.End);
      }

      if (syntax is LoopExpressionSyntax loopExpression)
      {
        var start = loopExpression.Label?.LabelToken.Span.Start ??
            loopExpression.LoopKeyword.Span.Start;
        return TextSpan.FromBounds(
            start,
            loopExpression.Body.CloseBraceToken.Span.End);
      }

      if (syntax is StringLiteralExpressionSyntax stringLiteralExpression)
        return stringLiteralExpression.StringToken.Span;

      if (syntax is IntegerLiteralExpressionSyntax integerLiteralExpression)
        return integerLiteralExpression.LiteralToken.Span;

      if (syntax is FloatLiteralExpressionSyntax floatLiteralExpression)
        return floatLiteralExpression.LiteralToken.Span;

      if (syntax is CharacterLiteralExpressionSyntax characterLiteralExpression)
        return characterLiteralExpression.LiteralToken.Span;

      if (syntax is BooleanLiteralExpressionSyntax booleanLiteralExpression)
        return booleanLiteralExpression.LiteralToken.Span;

      if (syntax is NullLiteralExpressionSyntax nullLiteralExpression)
        return nullLiteralExpression.NullToken.Span;

      if (syntax is ArrayLiteralExpressionSyntax arrayLiteralExpression)
      {
        return TextSpan.FromBounds(
            arrayLiteralExpression.OpenBracketToken.Span.Start,
            arrayLiteralExpression.CloseBracketToken.Span.End);
      }

      if (syntax is NameExpressionSyntax nameExpression)
      {
        if (nameExpression.QuestionToken == null)
          return nameExpression.IdentifierToken.Span;

        return TextSpan.FromBounds(
            nameExpression.IdentifierToken.Span.Start,
            nameExpression.QuestionToken.Span.End);
      }

      if (syntax is MemberAccessExpressionSyntax memberAccessExpression)
      {
        var leftSpan = GetExpressionSpan(memberAccessExpression.Expression);
        return TextSpan.FromBounds(
            leftSpan.Start,
            memberAccessExpression.QuestionToken?.Span.End ??
                memberAccessExpression.Name.Span.End);
      }

      if (syntax is CallExpressionSyntax callExpression)
      {
        var targetSpan = GetExpressionSpan(callExpression.Target);
        return TextSpan.FromBounds(targetSpan.Start, callExpression.CloseParenToken.Span.End);
      }

      return new TextSpan(0, 0);
    }

    private static string UnquoteString(string tokenText)
    {
      if (string.IsNullOrEmpty(tokenText))
        return string.Empty;

      if (tokenText.Length < 2)
        return tokenText;

      if (tokenText[0] != '"' || tokenText[^1] != '"')
        return tokenText;

      return tokenText.Substring(1, tokenText.Length - 2);
    }

    private enum NumericCategory
    {
      SignedInteger,
      UnsignedInteger,
      FloatingPoint
    }

    private enum LoopBreakKind
    {
      None,
      Empty,
      Value
    }

    private sealed class LoopBindingContext
    {
      public LoopBindingContext(LoopSymbol symbol)
      {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
      }

      public LoopSymbol Symbol { get; }
      public bool HasReachableBreak { get; set; }
      public LoopBreakKind BreakKind { get; set; }
      public TypeSymbol BreakType { get; set; }
    }

    private sealed class BoundScope
    {
      private readonly List<LocalVariableSymbol> _locals = new();
      private readonly List<ParameterSymbol> _parameters = new();

      public BoundScope(BoundScope parent)
      {
        Parent = parent;
      }

      public BoundScope Parent { get; }

      public void Declare(LocalVariableSymbol local)
      {
        if (local == null)
          throw new ArgumentNullException(nameof(local));

        _locals.Add(local);
      }

      public void DeclareParameter(ParameterSymbol parameter)
      {
        if (parameter == null)
          throw new ArgumentNullException(nameof(parameter));

        _parameters.Add(parameter);
      }

      public bool TryLookupLocal(string name, out LocalVariableSymbol local)
      {
        for (var index = _locals.Count - 1; index >= 0; index--)
        {
          if (_locals[index].Name == name)
          {
            local = _locals[index];
            return true;
          }
        }

        if (Parent != null)
          return Parent.TryLookupLocal(name, out local);

        local = null;
        return false;
      }

      public bool TryLookupSymbol(string name, out Symbol symbol)
      {
        if (TryLookupLocal(name, out var local))
        {
          symbol = local;
          return true;
        }

        for (var index = _parameters.Count - 1; index >= 0; index--)
        {
          if (_parameters[index].Name == name)
          {
            symbol = _parameters[index];
            return true;
          }
        }

        if (Parent != null)
          return Parent.TryLookupSymbol(name, out symbol);

        symbol = null;
        return false;
      }
    }
  }
}
