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
    private static readonly MethodSymbol DebugLogMethod = new(
        "Debug.Log",
        new[] { TypeSymbol.String },
        TypeSymbol.U0,
        "UnityEngineDebug.__Log__SystemObject__SystemVoid");

    public DiagnosticBag Diagnostics { get; } = new();

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

      Diagnostics.ReportUnsupportedStatement(GetStatementSpan(syntax), syntax.GetType().Name);
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
        return BoundNullLiteralExpression.Instance;

      if (syntax is ArrayLiteralExpressionSyntax arrayLiteralExpression)
        return BindArrayLiteralExpression(arrayLiteralExpression);

      if (syntax is NameExpressionSyntax nameExpression)
        return BindNameExpression(nameExpression);

      if (syntax is MemberAccessExpressionSyntax memberAccessExpression)
        return BindMemberAccessExpression(memberAccessExpression);

      if (syntax is CallExpressionSyntax callExpression)
        return BindCallExpression(callExpression);

      Diagnostics.ReportUnsupportedExpression(GetExpressionSpan(syntax), syntax.GetType().Name);
      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindStringLiteralExpression(StringLiteralExpressionSyntax syntax)
    {
      var value = syntax.StringToken.Value as string
          ?? UnquoteString(syntax.StringToken.Text ?? "");
      return new BoundStringLiteralExpression(value);
    }

    private BoundExpression BindIntegerLiteralExpression(IntegerLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Kind == SyntaxKind.Int32Literal &&
          syntax.LiteralToken.Value is int intValue)
      {
        return new BoundInt32LiteralExpression(intValue);
      }

      if (syntax.LiteralToken.Kind == SyntaxKind.UInt32Literal &&
          syntax.LiteralToken.Value is uint uintValue)
      {
        return new BoundUInt32LiteralExpression(uintValue);
      }

      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindFloatLiteralExpression(FloatLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Value is float floatValue)
        return new BoundFloat32LiteralExpression(floatValue);

      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindBooleanLiteralExpression(BooleanLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Value is bool boolValue)
        return new BoundBooleanLiteralExpression(boolValue);

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
      if (name == "Debug")
        return new BoundNameExpression(name, TypeSymbol.Debug);

      Diagnostics.ReportUndefinedName(syntax.IdentifierToken.Span, name);
      return BoundErrorExpression.Instance;
    }

    private BoundExpression BindMemberAccessExpression(MemberAccessExpressionSyntax syntax)
    {
      var receiver = BindExpression(syntax.Expression);
      var memberName = syntax.Name.Text ?? "";

      if (receiver.Type == TypeSymbol.Debug && memberName == "Log")
      {
        return new BoundMemberAccessExpression(
            receiver,
            memberName,
            DebugLogMethod,
            TypeSymbol.Method);
      }

      if (receiver.Type != TypeSymbol.Error)
        Diagnostics.ReportUndefinedMember(syntax.Name.Span, receiver.Type.Name, memberName);

      return new BoundMemberAccessExpression(
          receiver,
          memberName,
          null,
          TypeSymbol.Error);
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax syntax)
    {
      var target = BindExpression(syntax.Target);
      var arguments = new List<BoundExpression>();

      foreach (var argument in syntax.Arguments)
        arguments.Add(BindExpression(argument));

      if (target is BoundMemberAccessExpression memberAccess && memberAccess.Method != null)
        return BindMethodCall(syntax, target, memberAccess.Method, arguments);

      Diagnostics.ReportUnsupportedCallTarget(GetExpressionSpan(syntax.Target));
      return new BoundCallExpression(target, arguments, null, TypeSymbol.Error);
    }

    private BoundExpression BindMethodCall(
        CallExpressionSyntax syntax,
        BoundExpression target,
        MethodSymbol method,
        IReadOnlyList<BoundExpression> arguments)
    {
      var hasError = false;

      if (arguments.Count != method.Parameters.Count)
      {
        Diagnostics.ReportInvalidArgumentCount(
            GetExpressionSpan(syntax),
            method.Name,
            method.Parameters.Count,
            arguments.Count);
        hasError = true;
      }

      var parameterCount = Math.Min(arguments.Count, method.Parameters.Count);
      for (var index = 0; index < parameterCount; index++)
      {
        var argument = arguments[index];
        var parameterType = method.Parameters[index];

        if (argument.Type == TypeSymbol.Error)
          continue;

        if (!CanAssign(parameterType, argument.Type))
        {
          Diagnostics.ReportTypeMismatch(
              GetExpressionSpan(syntax.Arguments[index]),
              parameterType.Name,
              argument.Type.Name);
          hasError = true;
        }
      }

      return new BoundCallExpression(
          target,
          arguments,
          method,
          hasError ? TypeSymbol.Error : method.ReturnType);
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
        return TextSpan.FromBounds(expressionSpan.Start, expressionStatement.SemicolonToken.Span.End);
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
