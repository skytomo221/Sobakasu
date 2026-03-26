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
    private readonly SobakasuCompilationEnvironment _environment;

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
      var events = new List<BoundEventDeclaration>();

      foreach (var member in syntax.Members)
      {
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

      foreach (var statement in syntax.Statements)
        statements.Add(BindStatement(statement));

      return new BoundBlockStatement(statements);
    }

    private BoundStatement BindStatement(StatementSyntax syntax)
    {
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

    private BoundExpression BindExpression(ExpressionSyntax syntax)
    {
      if (syntax is StringLiteralExpressionSyntax stringLiteralExpression)
        return BindStringLiteralExpression(stringLiteralExpression);

      if (syntax is IntegerLiteralExpressionSyntax integerLiteralExpression)
        return BindIntegerLiteralExpression(integerLiteralExpression);

      if (syntax is FloatLiteralExpressionSyntax floatLiteralExpression)
        return BindFloatLiteralExpression(floatLiteralExpression);

      if (syntax is BooleanLiteralExpressionSyntax booleanLiteralExpression)
        return BindBooleanLiteralExpression(booleanLiteralExpression);

      if (syntax is NullLiteralExpressionSyntax)
        return new BoundLiteralExpression(null, TypeSymbol.Null);

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

    private BoundExpression BindStringLiteralExpression(StringLiteralExpressionSyntax syntax)
    {
      var value = syntax.StringToken.Value as string
          ?? UnquoteString(syntax.StringToken.Text ?? "");
      return new BoundLiteralExpression(value, TypeSymbol.String);
    }

    private BoundExpression BindIntegerLiteralExpression(IntegerLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Kind == SyntaxKind.Int32Literal &&
          syntax.LiteralToken.Value is int intValue)
      {
        return new BoundLiteralExpression(intValue, TypeSymbol.I32);
      }

      if (syntax.LiteralToken.Kind == SyntaxKind.UInt32Literal &&
          syntax.LiteralToken.Value is uint uintValue)
      {
        return new BoundLiteralExpression(uintValue, TypeSymbol.U32);
      }

      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindFloatLiteralExpression(FloatLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Value is float floatValue)
        return new BoundLiteralExpression(floatValue, TypeSymbol.F32);

      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindBooleanLiteralExpression(BooleanLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Value is bool boolValue)
        return new BoundLiteralExpression(boolValue, TypeSymbol.Bool);

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
      var name = syntax.IdentifierToken.Text ?? "";
      var symbol = _environment.GlobalNamespace.Lookup(name);
      if (symbol == null)
      {
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
      var memberName = syntax.Name.Text ?? "";

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

      var selectedMethod = SelectBestOverload(applicableMethods, arguments);
      return new BoundCallExpression(
          target,
          arguments,
          selectedMethod,
          selectedMethod.ReturnType);
    }

    private Symbol LookupMember(BoundExpression receiver, string memberName)
    {
      var receiverSymbol = GetReferencedSymbol(receiver);
      if (receiverSymbol is NamespaceSymbol namespaceSymbol)
        return namespaceSymbol.Lookup(memberName);

      var methods = receiver.Type.GetMethods(memberName);
      if (methods.Count > 0)
        return new MethodGroupSymbol(memberName, receiver.Type, methods);

      return null;
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
        if (!CanAssign(method.Parameters[index].Type, arguments[index].Type))
          return false;
      }

      return true;
    }

    private static MethodSymbol SelectBestOverload(
        IReadOnlyList<MethodSymbol> methods,
        IReadOnlyList<BoundExpression> arguments)
    {
      foreach (var method in methods)
      {
        var isExactMatch = true;
        for (var index = 0; index < arguments.Count; index++)
        {
          if (method.Parameters[index].Type != arguments[index].Type)
          {
            isExactMatch = false;
            break;
          }
        }

        if (isExactMatch)
          return method;
      }

      return methods[0];
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
      foreach (var element in elements)
      {
        if (element.Type != TypeSymbol.Error && element.Type != TypeSymbol.Null)
          return element.Type;
      }

      return null;
    }

    private static bool CanAssign(TypeSymbol targetType, TypeSymbol sourceType)
    {
      if (targetType == TypeSymbol.Error || sourceType == TypeSymbol.Error)
        return true;

      if (targetType == sourceType)
        return true;

      return sourceType == TypeSymbol.Null && targetType.IsReferenceType;
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
      if (syntax is StringLiteralExpressionSyntax stringLiteralExpression)
        return stringLiteralExpression.StringToken.Span;

      if (syntax is IntegerLiteralExpressionSyntax integerLiteralExpression)
        return integerLiteralExpression.LiteralToken.Span;

      if (syntax is FloatLiteralExpressionSyntax floatLiteralExpression)
        return floatLiteralExpression.LiteralToken.Span;

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
  }
}
