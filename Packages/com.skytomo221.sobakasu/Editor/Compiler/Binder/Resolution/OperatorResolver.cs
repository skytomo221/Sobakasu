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
  internal sealed class OperatorResolver : BinderComponent
  {
    internal OperatorResolver(BindingSession session) : base(session)
    {
    }
  
    internal BoundBinaryOperator BindAbiBinaryOperator(SyntaxKind operatorKind, TypeSymbol leftType, TypeSymbol rightType, TextSpan span, bool reportDiagnostics = true)
    {
      switch (operatorKind)
      {
        case SyntaxKind.AmpersandAmpersandToken:
          if (leftType != TypeSymbol.Bool || rightType != TypeSymbol.Bool)
          {
            Session.Diagnostics.ReportShortCircuitRequiresBoolOperands(span, Session.OperatorResolver.GetOperatorText(operatorKind), leftType.Name, rightType.Name);
            return null;
          }
  
          return new BoundBinaryOperator(BoundBinaryOperatorKind.LogicalAnd, operatorKind, leftType, rightType, TypeSymbol.Bool);
        case SyntaxKind.PipePipeToken:
          if (leftType != TypeSymbol.Bool || rightType != TypeSymbol.Bool)
          {
            Session.Diagnostics.ReportShortCircuitRequiresBoolOperands(span, Session.OperatorResolver.GetOperatorText(operatorKind), leftType.Name, rightType.Name);
            return null;
          }
  
          return new BoundBinaryOperator(BoundBinaryOperatorKind.LogicalOr, operatorKind, leftType, rightType, TypeSymbol.Bool);
        case SyntaxKind.PlusToken when leftType == rightType && Session.OperatorResolver.IsNumericType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.Addition, operatorKind, leftType, rightType, leftType, "op_Addition", span);
        case SyntaxKind.MinusToken when leftType == rightType && Session.OperatorResolver.IsNumericType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.Subtraction, operatorKind, leftType, rightType, leftType, "op_Subtraction", span);
        case SyntaxKind.StarToken when leftType == rightType && Session.OperatorResolver.IsNumericType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.Multiplication, operatorKind, leftType, rightType, leftType, "op_Multiply", span);
        case SyntaxKind.SlashToken when leftType == rightType && Session.OperatorResolver.IsNumericType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.Division, operatorKind, leftType, rightType, leftType, "op_Division", span);
        case SyntaxKind.PercentToken when leftType == rightType && Session.OperatorResolver.IsNumericType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.Modulus, operatorKind, leftType, rightType, leftType, "op_Modulus", span);
        case SyntaxKind.EqualsEqualsToken when leftType == rightType && Session.OperatorResolver.IsEqualityPrimitiveType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.Equals, operatorKind, leftType, rightType, TypeSymbol.Bool, "op_Equality", span);
        case SyntaxKind.BangEqualsToken when leftType == rightType && Session.OperatorResolver.IsEqualityPrimitiveType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.NotEquals, operatorKind, leftType, rightType, TypeSymbol.Bool, "op_Inequality", span);
        case SyntaxKind.LessToken when leftType == rightType && Session.OperatorResolver.IsNumericType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.Less, operatorKind, leftType, rightType, TypeSymbol.Bool, "op_LessThan", span);
        case SyntaxKind.LessOrEqualsToken when leftType == rightType && Session.OperatorResolver.IsNumericType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.LessOrEquals, operatorKind, leftType, rightType, TypeSymbol.Bool, "op_LessThanOrEqual", span);
        case SyntaxKind.GreaterToken when leftType == rightType && Session.OperatorResolver.IsNumericType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.Greater, operatorKind, leftType, rightType, TypeSymbol.Bool, "op_GreaterThan", span);
        case SyntaxKind.GreaterOrEqualsToken when leftType == rightType && Session.OperatorResolver.IsNumericType(leftType):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.GreaterOrEquals, operatorKind, leftType, rightType, TypeSymbol.Bool, "op_GreaterThanOrEqual", span);
        case SyntaxKind.AmpersandToken when leftType == rightType && (Session.OperatorResolver.IsIntegerType(leftType) || leftType == TypeSymbol.Bool):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.BitwiseAnd, operatorKind, leftType, rightType, leftType, "op_BitwiseAnd", span);
        case SyntaxKind.PipeToken when leftType == rightType && (Session.OperatorResolver.IsIntegerType(leftType) || leftType == TypeSymbol.Bool):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.BitwiseOr, operatorKind, leftType, rightType, leftType, "op_BitwiseOr", span);
        case SyntaxKind.CaretToken when leftType == rightType && (Session.OperatorResolver.IsIntegerType(leftType) || leftType == TypeSymbol.Bool):
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.BitwiseXor, operatorKind, leftType, rightType, leftType, "op_ExclusiveOr", span);
        case SyntaxKind.LessLessToken when Session.OperatorResolver.IsIntegerType(leftType) && rightType == TypeSymbol.I32:
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.LeftShift, operatorKind, leftType, rightType, leftType, "op_LeftShift", span);
        case SyntaxKind.GreaterGreaterToken when Session.OperatorResolver.IsIntegerType(leftType) && rightType == TypeSymbol.I32:
          return Session.OperatorResolver.CreateBinaryOperator(BoundBinaryOperatorKind.RightShift, operatorKind, leftType, rightType, leftType, "op_RightShift", span);
      }
  
      if (reportDiagnostics)
      {
        Session.Diagnostics.ReportUnsupportedBinaryOperator(span, Session.OperatorResolver.GetOperatorText(operatorKind), leftType.Name, rightType.Name);
      }
  
      return null;
    }
  
    internal BoundUnaryOperator CreateUnaryOperator(BoundUnaryOperatorKind kind, SyntaxKind operatorKind, TypeSymbol operandType, TypeSymbol resultType, string methodName, TextSpan span)
    {
      if (!Session.OperatorResolver.TryResolveUnaryOperatorSignature(methodName, operatorKind, operandType, resultType, span, out var externSignature, out var wasAmbiguous))
      {
        if (!wasAmbiguous)
        {
          Session.Diagnostics.ReportUnsupportedUnaryOperator(span, Session.OperatorResolver.GetOperatorText(operatorKind), operandType.Name);
        }
  
        return null;
      }
  
      return new BoundUnaryOperator(kind, operatorKind, operandType, resultType, externSignature);
    }
  
    internal BoundBinaryOperator CreateBinaryOperator(BoundBinaryOperatorKind kind, SyntaxKind operatorKind, TypeSymbol leftType, TypeSymbol rightType, TypeSymbol resultType, string methodName, TextSpan span)
    {
      if (!Session.OperatorResolver.TryResolveBinaryOperatorSignature(methodName, operatorKind, leftType, rightType, resultType, span, out var externSignature, out var wasAmbiguous))
      {
        if (!wasAmbiguous)
        {
          Session.Diagnostics.ReportUnsupportedBinaryOperator(span, Session.OperatorResolver.GetOperatorText(operatorKind), leftType.Name, rightType.Name);
        }
  
        return null;
      }
  
      return new BoundBinaryOperator(kind, operatorKind, leftType, rightType, resultType, externSignature);
    }
  
    internal bool TryResolveUnaryOperatorSignature(string methodName, SyntaxKind operatorKind, TypeSymbol operandType, TypeSymbol resultType, TextSpan span, out string externSignature, out bool wasAmbiguous)
    {
      var candidates = Session.Environment.ExternCatalog.GetUnaryOperatorSignatures(methodName, operandType, resultType);
      return Session.OperatorResolver.TryResolveOperatorSignature(candidates, operatorKind, span, operandType.Name, out externSignature, out wasAmbiguous);
    }
  
    internal bool TryResolveBinaryOperatorSignature(string methodName, SyntaxKind operatorKind, TypeSymbol leftType, TypeSymbol rightType, TypeSymbol resultType, TextSpan span, out string externSignature, out bool wasAmbiguous)
    {
      var candidates = Session.Environment.ExternCatalog.GetBinaryOperatorSignatures(methodName, leftType, rightType, resultType);
      return Session.OperatorResolver.TryResolveOperatorSignature(candidates, operatorKind, span, $"{leftType.Name}, {rightType.Name}", out externSignature, out wasAmbiguous);
    }
  
    internal bool TryResolveOperatorSignature(IReadOnlyList<string> candidates, SyntaxKind operatorKind, TextSpan span, string operandTypes, out string externSignature, out bool wasAmbiguous)
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
        Session.Diagnostics.ReportAmbiguousOperator(span, Session.OperatorResolver.GetOperatorText(operatorKind), operandTypes, string.Join(", ", candidates));
        return false;
      }
  
      return false;
    }
  
    internal SyntaxKind? GetBinaryOperatorKindForCompoundAssignment(SyntaxKind operatorKind)
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
  
    internal bool IsNumericType(TypeSymbol type)
    {
      return Session.ConversionClassifier.TryGetNumericCategoryAndRank(type, out _, out _);
    }
  
    internal bool IsIntegerType(TypeSymbol type)
    {
      return type.TypeKind is TypeKind.I8 or TypeKind.U8 or TypeKind.I16 or TypeKind.U16 or TypeKind.I32 or TypeKind.U32 or TypeKind.I64 or TypeKind.U64;
    }
  
    internal bool IsEqualityPrimitiveType(TypeSymbol type)
    {
      return type == TypeSymbol.Bool || type == TypeSymbol.Char || type == TypeSymbol.String || Session.OperatorResolver.IsNumericType(type);
    }
  
    internal string GetOperatorText(SyntaxKind kind)
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
        _ => kind.ToString()};
    }
  
    internal string GetAssignmentTargetDisplayText(ExpressionSyntax syntax)
    {
      if (syntax is NameExpressionSyntax nameExpression)
        return nameExpression.Name;
      if (syntax is MemberAccessExpressionSyntax memberAccessExpression)
        return memberAccessExpression.Name.Text ?? "<member>";
      if (syntax is ElementAccessExpressionSyntax)
        return "array element";
      return syntax.GetType().Name;
    }
  }
}
