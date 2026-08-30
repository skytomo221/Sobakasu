using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class OperatorExpressionBinder : BinderComponent
  {
    internal OperatorExpressionBinder(BindingSession session) : base(session)
    {
    }
  
    internal BoundExpression BindUnaryExpression(UnaryExpressionSyntax syntax)
    {
      var operand = Session.ExpressionBinder.BindExpression(syntax.Operand);
      if (operand.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;
      var span = Session.BinderSyntaxFacts.GetExpressionSpan(syntax);
      switch (syntax.OperatorToken.Kind)
      {
        case SyntaxKind.PlusToken:
          if (Session.OperatorResolver.IsNumericType(operand.Type))
            return operand;
          break;
        case SyntaxKind.MinusToken:
          if (Session.OperatorResolver.IsNumericType(operand.Type))
          {
            var zeroLiteral = Session.OperatorResolver.CreateZeroLiteral(operand.Type, span);
            var subtractionOperator = Session.OperatorResolver.BindBinaryOperator(SyntaxKind.MinusToken, zeroLiteral.Type, operand.Type, span);
            if (subtractionOperator == null)
              return BoundErrorExpression.Instance;
            return new BoundBinaryExpression(zeroLiteral, subtractionOperator, operand);
          }
  
          break;
        case SyntaxKind.TildeToken:
          if (Session.OperatorResolver.IsIntegerType(operand.Type))
          {
            var allBitsSetLiteral = Session.OperatorResolver.CreateAllBitsSetLiteral(operand.Type, span);
            var xorOperator = Session.OperatorResolver.BindBinaryOperator(SyntaxKind.CaretToken, operand.Type, allBitsSetLiteral.Type, span);
            if (xorOperator == null)
              return BoundErrorExpression.Instance;
            return new BoundBinaryExpression(operand, xorOperator, allBitsSetLiteral);
          }
  
          break;
        case SyntaxKind.BangToken when operand.Type == TypeSymbol.Bool:
          var builtInOperator = Session.OperatorResolver.BindUnaryOperator(syntax.OperatorToken.Kind, operand.Type, span);
          if (builtInOperator == null)
            return BoundErrorExpression.Instance;
          return new BoundUnaryExpression(builtInOperator, operand);
      }
  
      var userOperator = Session.OperatorExpressionBinder.BindUserDefinedOperatorCall(syntax.OperatorToken.Kind, operand, null, isUnary: true, span);
      if (userOperator != null)
        return userOperator;
      Session.Diagnostics.ReportUnsupportedUnaryOperator(span, Session.OperatorResolver.GetOperatorText(syntax.OperatorToken.Kind), operand.Type.Name);
      return BoundErrorExpression.Instance;
    }
  
    internal BoundExpression BindBinaryExpression(BinaryExpressionSyntax syntax)
    {
      var left = Session.ExpressionBinder.BindExpression(syntax.Left);
      var right = Session.ExpressionBinder.BindExpression(syntax.Right);
      if (left.Type == TypeSymbol.Error || right.Type == TypeSymbol.Error)
        return BoundErrorExpression.Instance;
      var boundOperator = Session.OperatorResolver.BindBinaryOperator(syntax.OperatorToken.Kind, left.Type, right.Type, Session.BinderSyntaxFacts.GetExpressionSpan(syntax), reportDiagnostics: false);
      if (boundOperator == null)
      {
        var userOperator = Session.OperatorExpressionBinder.BindUserDefinedOperatorCall(syntax.OperatorToken.Kind, left, right, isUnary: false, Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
        if (userOperator != null)
          return userOperator;
        Session.Diagnostics.ReportUnsupportedBinaryOperator(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), Session.OperatorResolver.GetOperatorText(syntax.OperatorToken.Kind), left.Type.Name, right.Type.Name);
        return BoundErrorExpression.Instance;
      }
  
      return new BoundBinaryExpression(left, boundOperator, right);
    }
  
    internal BoundExpression BindUserDefinedOperatorCall(SyntaxKind operatorKind, BoundExpression left, BoundExpression right, bool isUnary, TextSpan span)
    {
      var name = (isUnary ? "@" : string.Empty) + Session.OperatorResolver.GetOperatorText(operatorKind);
      if (!Session.Declarations.MethodGroupsByType.TryGetValue(left.Type, out var groups) || !groups.TryGetValue(name, out var group))
      {
        return null;
      }
  
      var arguments = isUnary ? Array.Empty<BoundExpression>() : new[]
      {
        right
      };
      var applicable = new List<MethodSymbol>();
      var hasInaccessibleUserMethod = false;
      foreach (var method in group.Methods)
      {
        if (method is not UserMethodSymbol userMethod)
          continue;
        if (!Session.VisibilityResolver.IsUserMethodVisible(userMethod))
        {
          hasInaccessibleUserMethod = true;
          continue;
        }
  
        if (!userMethod.IsStatic && method.Parameters.Count == arguments.Length && Session.OverloadResolver.IsApplicable(method, arguments))
        {
          applicable.Add(method);
        }
      }
  
      if (applicable.Count == 0)
      {
        if (hasInaccessibleUserMethod)
        {
          Session.Diagnostics.ReportDeclarationNotPublic(span, group.Name);
        }
        else
        {
          Session.Diagnostics.ReportNoApplicableMethodOverload(span, group.DisplayName, Session.OverloadResolver.BuildArgumentTypeList(arguments));
        }
  
        return BoundErrorExpression.Instance;
      }
  
      var selected = Session.OverloadResolver.SelectBestOverload(applicable, arguments, out var ambiguous);
      if (ambiguous || selected is not UserMethodSymbol selectedUserMethod)
      {
        Session.Diagnostics.ReportAmbiguousMethodOverload(span, group.DisplayName, Session.OverloadResolver.BuildMethodCandidateList(applicable));
        return BoundErrorExpression.Instance;
      }
  
      return new BoundUserFunctionCallExpression(selectedUserMethod.Function, arguments, left);
    }
  
    internal BoundExpression BindStringLiteralExpression(StringLiteralExpressionSyntax syntax)
    {
      var value = syntax.StringToken.Value as string ?? Session.BinderSyntaxFacts.UnquoteString(syntax.StringToken.Text ?? "");
      return new BoundLiteralExpression(value, TypeSymbol.String, syntax.StringToken.Span);
    }
  
    internal BoundExpression BindIntegerLiteralExpression(IntegerLiteralExpressionSyntax syntax)
    {
      return syntax.LiteralToken.Kind switch
      {
        SyntaxKind.Int8Literal when syntax.LiteralToken.Value is sbyte int8Value => new BoundLiteralExpression(int8Value, TypeSymbol.I8, syntax.LiteralToken.Span),
        SyntaxKind.UInt8Literal when syntax.LiteralToken.Value is byte uint8Value => new BoundLiteralExpression(uint8Value, TypeSymbol.U8, syntax.LiteralToken.Span),
        SyntaxKind.Int16Literal when syntax.LiteralToken.Value is short int16Value => new BoundLiteralExpression(int16Value, TypeSymbol.I16, syntax.LiteralToken.Span),
        SyntaxKind.UInt16Literal when syntax.LiteralToken.Value is ushort uint16Value => new BoundLiteralExpression(uint16Value, TypeSymbol.U16, syntax.LiteralToken.Span),
        SyntaxKind.Int32Literal when syntax.LiteralToken.Value is int int32Value => new BoundLiteralExpression(int32Value, TypeSymbol.I32, syntax.LiteralToken.Span),
        SyntaxKind.UInt32Literal when syntax.LiteralToken.Value is uint uint32Value => new BoundLiteralExpression(uint32Value, TypeSymbol.U32, syntax.LiteralToken.Span),
        SyntaxKind.Int64Literal when syntax.LiteralToken.Value is long int64Value => new BoundLiteralExpression(int64Value, TypeSymbol.I64, syntax.LiteralToken.Span),
        SyntaxKind.UInt64Literal when syntax.LiteralToken.Value is ulong uint64Value => new BoundLiteralExpression(uint64Value, TypeSymbol.U64, syntax.LiteralToken.Span),
        _ => BoundErrorExpression.Instance
      };
    }
  
    internal BoundExpression BindFloatLiteralExpression(FloatLiteralExpressionSyntax syntax)
    {
      return syntax.LiteralToken.Kind switch
      {
        SyntaxKind.Float32Literal when syntax.LiteralToken.Value is float floatValue => new BoundLiteralExpression(floatValue, TypeSymbol.F32, syntax.LiteralToken.Span),
        SyntaxKind.Float64Literal when syntax.LiteralToken.Value is double doubleValue => new BoundLiteralExpression(doubleValue, TypeSymbol.F64, syntax.LiteralToken.Span),
        _ => BoundErrorExpression.Instance
      };
    }
  
    internal BoundExpression BindCharacterLiteralExpression(CharacterLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Value is char charValue)
        return new BoundLiteralExpression(charValue, TypeSymbol.Char, syntax.LiteralToken.Span);
      return BoundErrorExpression.Instance;
    }
  
    internal BoundExpression BindBooleanLiteralExpression(BooleanLiteralExpressionSyntax syntax)
    {
      if (syntax.LiteralToken.Value is bool boolValue)
        return new BoundLiteralExpression(boolValue, TypeSymbol.Bool, syntax.LiteralToken.Span);
      return BoundErrorExpression.Instance;
    }
  }
}
