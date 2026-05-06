using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
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
    private readonly List<UseDirectiveBinding> _useBindings = new();
    private ImportScope _importScope = new();

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
      _useBindings.Clear();
      _importScope = new ImportScope();

      foreach (var member in syntax.Members)
      {
        if (member is UseDirectiveSyntax useDirective)
          BindUseDirective(useDirective);
      }

      var events = new List<BoundEventDeclaration>();

      foreach (var member in syntax.Members)
      {
        if (member is UseDirectiveSyntax)
          continue;

        if (member is EventDeclarationSyntax eventDeclaration)
        {
          events.Add(BindEventDeclaration(eventDeclaration));
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
            syntax.EndOfFileToken.Span,
            member.GetType().Name);
      }

      return new BoundProgram(events);
    }

    private void BindUseDirective(UseDirectiveSyntax syntax)
    {
      if (syntax == null)
        throw new ArgumentNullException(nameof(syntax));

      if (syntax.IsMalformed)
        return;

      var importedPath = syntax.Path?.GetText() ?? string.Empty;
      var directiveSpan = GetUseDirectiveSpan(syntax);

      if (!TryResolveUseTarget(syntax.Path, out var importedSymbol, out var unsupportedReason))
      {
        if (!string.IsNullOrEmpty(unsupportedReason))
        {
          Diagnostics.ReportUnsupportedUseTarget(
              directiveSpan,
              importedPath,
              unsupportedReason);
        }
        else
        {
          Diagnostics.ReportUnresolvedUsePath(directiveSpan, importedPath);
        }

        return;
      }

      if (syntax.Alias != null && importedSymbol is NamespaceSymbol)
      {
        Diagnostics.ReportUnsupportedUseTarget(
            directiveSpan,
            importedPath,
            "Namespace aliases are not supported in v1.");
        return;
      }

      var introducedName = syntax.Alias?.Text;
      if (string.IsNullOrEmpty(introducedName))
        introducedName = importedSymbol.Name;

      var binding = new UseDirectiveBinding(
          importedPath,
          introducedName,
          importedSymbol,
          syntax.Alias != null);
      _useBindings.Add(binding);

      if (importedSymbol is NamespaceSymbol)
      {
        _importScope.TryAddNamespaceImport(binding);
        return;
      }

      if (binding.IsAlias)
      {
        _importScope.TryAddAlias(binding, Diagnostics, directiveSpan);
      }
      else
      {
        _importScope.TryAddDirectImport(binding, Diagnostics, directiveSpan);
      }
    }

    private bool TryResolveUseTarget(
        QualifiedNameSyntax path,
        out Symbol symbol,
        out string unsupportedReason)
    {
      symbol = _environment.GlobalNamespace;
      unsupportedReason = null;

      if (path == null || path.Identifiers.Count == 0)
        return false;

      for (var index = 0; index < path.Identifiers.Count; index++)
      {
        var segment = path.Identifiers[index].Text ?? string.Empty;
        var isLastSegment = index == path.Identifiers.Count - 1;

        if (symbol is NamespaceSymbol namespaceSymbol)
        {
          symbol = namespaceSymbol.Lookup(segment);
          if (symbol == null)
            return false;

          continue;
        }

        if (symbol is TypeSymbol typeSymbol && isLastSegment)
        {
          var methodGroup = typeSymbol.GetMethodGroup(segment);
          if (methodGroup != null)
          {
            symbol = methodGroup;
            return true;
          }

          if (typeSymbol.TryGetUnsupportedImportMemberReason(segment, out unsupportedReason))
            return false;

          unsupportedReason = "Only static method groups can be imported from types in v1.";
          return false;
        }

        unsupportedReason =
            "Only namespaces, types, and terminal static method groups can be imported in v1.";
        symbol = null;
        return false;
      }

      return symbol is NamespaceSymbol || symbol is TypeSymbol || symbol is MethodGroupSymbol;
    }

    private BoundEventDeclaration BindEventDeclaration(EventDeclarationSyntax syntax)
    {
      var eventName = syntax.Identifier.Text ?? "";
      var exportName = BindExportName(syntax.Identifier.Span, eventName);
      var body = BindBlockStatement(syntax.Body);

      return new BoundEventDeclaration(eventName, exportName, body);
    }

    private string BindExportName(TextSpan span, string eventName)
    {
      if (eventName == "Interact")
        return "_interact";

      Diagnostics.ReportUnsupportedEventName(span, eventName);
      return "_invalid_event";
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
      }
      finally
      {
        _scope = parentScope;
      }

      return new BoundBlockStatement(statements);
    }

    private BoundStatement BindStatement(StatementSyntax syntax)
    {
      if (syntax is VariableDeclarationStatementSyntax variableDeclarationStatement)
        return BindVariableDeclarationStatement(variableDeclarationStatement);

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
      var typeName = syntax.TypeIdentifier.Text ?? string.Empty;
      if (BuiltInTypes.TryGetValue(typeName, out var builtInType))
        return builtInType;

      var resolvedSymbol = ResolveVisibleSymbol(
          typeName,
          syntax.TypeIdentifier.Span,
          out var resolutionHadDiagnostic);
      if (resolvedSymbol is TypeSymbol typeSymbol)
        return typeSymbol;

      if (resolutionHadDiagnostic)
        return TypeSymbol.Error;

      Diagnostics.ReportUnknownType(syntax.TypeIdentifier.Span, typeName);
      return TypeSymbol.Error;
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

      Diagnostics.ReportUnsupportedExpression(
          GetExpressionSpan(syntax),
          syntax.GetType().Name);
      return BoundErrorExpression.Instance;
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

      var name = nameExpressionSyntax.IdentifierToken.Text ?? string.Empty;

      var local = LookupLocal(name);
      if (local == null)
      {
        var resolvedSymbol = ResolveVisibleSymbol(
            name,
            nameExpressionSyntax.IdentifierToken.Span,
            out var resolutionHadDiagnostic);
        if (resolutionHadDiagnostic)
          return BoundErrorExpression.Instance;

        if (resolvedSymbol != null)
        {
          if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
          {
            Diagnostics.ReportInvalidAssignmentTarget(
                nameExpressionSyntax.IdentifierToken.Span,
                name);
          }
          else
          {
            Diagnostics.ReportInvalidCompoundAssignmentTarget(
                nameExpressionSyntax.IdentifierToken.Span);
          }
        }
        else
        {
          Diagnostics.ReportUndefinedName(nameExpressionSyntax.IdentifierToken.Span, name);
        }

        return BoundErrorExpression.Instance;
      }

      if (!local.IsMutable)
      {
        Diagnostics.ReportCannotAssignToImmutableLocal(
            nameExpressionSyntax.IdentifierToken.Span,
            name);
      }

      if (expression.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;

      if (syntax.OperatorToken.Kind == SyntaxKind.EqualsToken)
      {
        if (!CanAssignToLocal(local.Type, expression.Type))
        {
          Diagnostics.ReportTypeMismatch(
              GetExpressionSpan(syntax.Expression),
              local.Type.Name,
              expression.Type.Name);
        }

        return new BoundAssignmentExpression(local, expression);
      }

      var binarySyntaxKind = GetBinaryOperatorKindForCompoundAssignment(syntax.OperatorToken.Kind);
      if (binarySyntaxKind == null)
      {
        Diagnostics.ReportInvalidCompoundAssignmentTarget(targetSpan);
        return BoundErrorExpression.Instance;
      }

      var left = new BoundNameExpression(name, local, local.Type);
      var boundOperator = BindBinaryOperator(
          binarySyntaxKind.Value,
          local.Type,
          expression.Type,
          GetExpressionSpan(syntax));
      if (boundOperator == null)
        return BoundErrorExpression.Instance;

      var valueExpression = new BoundBinaryExpression(left, boundOperator, expression);
      if (!CanAssignToLocal(local.Type, valueExpression.Type))
      {
        Diagnostics.ReportTypeMismatch(
            GetExpressionSpan(syntax),
            local.Type.Name,
            valueExpression.Type.Name);
      }

      return new BoundAssignmentExpression(local, valueExpression);
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
          if (!IsNumericType(operand.Type))
          {
            Diagnostics.ReportUnsupportedUnaryOperator(
                span,
                GetOperatorText(syntax.OperatorToken.Kind),
                operand.Type.Name);
            return BoundErrorExpression.Instance;
          }

          return operand;

        case SyntaxKind.MinusToken:
          if (!IsNumericType(operand.Type))
          {
            Diagnostics.ReportUnsupportedUnaryOperator(
                span,
                GetOperatorText(syntax.OperatorToken.Kind),
                operand.Type.Name);
            return BoundErrorExpression.Instance;
          }

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

        case SyntaxKind.TildeToken:
          if (!IsIntegerType(operand.Type))
          {
            Diagnostics.ReportUnsupportedUnaryOperator(
                span,
                GetOperatorText(syntax.OperatorToken.Kind),
                operand.Type.Name);
            return BoundErrorExpression.Instance;
          }

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

      var boundOperator = BindUnaryOperator(
          syntax.OperatorToken.Kind,
          operand.Type,
          span);
      if (boundOperator == null)
        return BoundErrorExpression.Instance;

      return new BoundUnaryExpression(boundOperator, operand);
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
          GetExpressionSpan(syntax));
      if (boundOperator == null)
        return BoundErrorExpression.Instance;

      return new BoundBinaryExpression(left, boundOperator, right);
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

    private BoundExpression BindNameExpression(NameExpressionSyntax syntax)
    {
      var name = syntax.IdentifierToken.Text ?? string.Empty;

      var local = LookupLocal(name);
      if (local != null)
      {
        return new BoundNameExpression(
            name,
            local,
            local.Type);
      }

      var symbol = ResolveVisibleSymbol(
          name,
          syntax.IdentifierToken.Span,
          out var resolutionHadDiagnostic);
      if (symbol == null)
      {
        if (resolutionHadDiagnostic)
          return new BoundNameExpression(name, null, TypeSymbol.Error);

        Diagnostics.ReportUndefinedName(syntax.IdentifierToken.Span, name);
        return new BoundNameExpression(name, null, TypeSymbol.Error);
      }

      return new BoundNameExpression(
          name,
          symbol,
          GetExpressionType(symbol));
    }

    private BoundExpression BindMemberAccessExpression(MemberAccessExpressionSyntax syntax)
    {
      var receiver = BindExpression(syntax.Expression);
      var memberName = syntax.Name.Text ?? string.Empty;

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

      return new BoundMemberAccessExpression(
          receiver,
          memberName,
          memberSymbol,
          GetExpressionType(memberSymbol));
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax syntax)
    {
      var target = BindExpression(syntax.Target);
      var arguments = new List<BoundExpression>();

      foreach (var argument in syntax.Arguments)
        arguments.Add(BindExpression(argument));

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

      if (methodGroup.Methods.Count == 0)
      {
        if (methodGroup.RejectedCandidates.Count > 0)
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
      foreach (var method in methodGroup.Methods)
      {
        if (method.Parameters.Count == arguments.Count)
          sameArityMethods.Add(method);
      }

      if (sameArityMethods.Count == 0)
      {
        var expectedCount = GetSharedParameterCount(methodGroup.Methods);
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
        Diagnostics.ReportNoMatchingOverload(
            GetExpressionSpan(syntax),
            methodGroup.DisplayName,
            BuildArgumentTypeList(arguments));
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
        Diagnostics.ReportAmbiguousExternOverload(
            GetExpressionSpan(syntax),
            methodGroup.DisplayName,
            BuildMethodCandidateList(applicableMethods));
        return new BoundCallExpression(
            target,
            arguments,
            null,
            TypeSymbol.Error);
      }

      return new BoundCallExpression(
          target,
          arguments,
          selectedMethod,
          selectedMethod.ReturnType);
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
        TextSpan span)
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

      Diagnostics.ReportUnsupportedBinaryOperator(
          span,
          GetOperatorText(operatorKind),
          leftType.Name,
          rightType.Name);
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
        return nameExpression.IdentifierToken.Text ?? "<name>";

      if (syntax is MemberAccessExpressionSyntax memberAccessExpression)
        return memberAccessExpression.Name.Text ?? "<member>";

      return syntax.GetType().Name;
    }

    private Symbol LookupMember(BoundExpression receiver, string memberName)
    {
      var receiverSymbol = GetReferencedSymbol(receiver);
      if (receiverSymbol is NamespaceSymbol namespaceSymbol)
        return namespaceSymbol.Lookup(memberName);

      if (receiverSymbol is TypeSymbol explicitTypeSymbol)
      {
        var explicitMethodGroup = explicitTypeSymbol.GetMethodGroup(memberName);
        if (explicitMethodGroup != null)
          return explicitMethodGroup;
      }

      var methods = receiver.Type.GetMethodGroup(memberName);
      if (methods != null)
        return methods;

      return null;
    }

    private LocalVariableSymbol LookupLocal(string name)
    {
      return _scope != null && _scope.TryLookup(name, out var local)
          ? local
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

      if (_importScope.TryResolveAlias(name, out var aliasSymbol))
        return aliasSymbol;

      var hasDirectImport = _importScope.TryResolveDirectImport(name, out var directImportSymbol);
      var namespaceCandidates = _importScope.GetNamespaceCandidates(name);

      if (hasDirectImport && namespaceCandidates.Count > 0)
      {
        Diagnostics.ReportAmbiguousImportedReference(
            span,
            name,
            BuildSymbolCandidateList(directImportSymbol, namespaceCandidates));
        resolutionHadDiagnostic = true;
        return null;
      }

      if (hasDirectImport)
        return directImportSymbol;

      if (namespaceCandidates.Count > 1)
      {
        Diagnostics.ReportAmbiguousImportedReference(
            span,
            name,
            BuildSymbolCandidateList(namespaceCandidates));
        resolutionHadDiagnostic = true;
        return null;
      }

      if (namespaceCandidates.Count == 1)
        return namespaceCandidates[0];

      var globalSymbol = _environment.GlobalNamespace.Lookup(name);
      if (globalSymbol != null)
        return globalSymbol;

      return _environment.TryLookupCompatibilitySymbol(name, out var compatibilitySymbol)
          ? compatibilitySymbol
          : null;
    }

    private static string BuildSymbolCandidateList(
        Symbol directSymbol,
        IReadOnlyList<Symbol> namespaceCandidates)
    {
      var candidates = new List<string>();
      candidates.Add(GetSymbolDisplayName(directSymbol));

      foreach (var namespaceCandidate in namespaceCandidates)
        candidates.Add(GetSymbolDisplayName(namespaceCandidate));

      return string.Join(", ", candidates);
    }

    private static string BuildSymbolCandidateList(IReadOnlyList<Symbol> symbols)
    {
      var candidates = new string[symbols.Count];
      for (var index = 0; index < symbols.Count; index++)
        candidates[index] = GetSymbolDisplayName(symbols[index]);

      return string.Join(", ", candidates);
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

      if (symbol is LocalVariableSymbol localVariableSymbol)
        return localVariableSymbol.Type;

      if (symbol is MethodGroupSymbol || symbol is MethodSymbol)
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
        bool allowObjectCatchAll,
        out int distance)
    {
      if (TryGetConversionDistance(targetType, sourceType, out distance))
        return true;

      if (allowObjectCatchAll &&
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

    private static TextSpan GetStatementSpan(StatementSyntax syntax)
    {
      if (syntax is ExpressionStatementSyntax expressionStatement)
      {
        var expressionSpan = GetExpressionSpan(expressionStatement.Expression);
        return TextSpan.FromBounds(
            expressionSpan.Start,
            expressionStatement.SemicolonToken.Span.End);
      }

      if (syntax is VariableDeclarationStatementSyntax variableDeclarationStatement)
      {
        return TextSpan.FromBounds(
            variableDeclarationStatement.LetKeyword.Span.Start,
            variableDeclarationStatement.SemicolonToken.Span.End);
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
        return nameExpression.IdentifierToken.Span;

      if (syntax is MemberAccessExpressionSyntax memberAccessExpression)
      {
        var leftSpan = GetExpressionSpan(memberAccessExpression.Expression);
        return TextSpan.FromBounds(leftSpan.Start, memberAccessExpression.Name.Span.End);
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

    private sealed class BoundScope
    {
      private readonly List<LocalVariableSymbol> _locals = new();

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

      public bool TryLookup(string name, out LocalVariableSymbol local)
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
          return Parent.TryLookup(name, out local);

        local = null;
        return false;
      }
    }
  }
}
