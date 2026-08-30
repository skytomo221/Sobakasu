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
  internal sealed class ExpressionBinder : BinderComponent
  {
    internal ExpressionBinder(BindingSession session) : base(session)
    {
    }
  
    internal BoundExpression BindExpression(ExpressionSyntax syntax, TypeSymbol expectedType = null)
    {
      if (syntax is AssignmentExpressionSyntax assignmentExpression)
        return Session.AssignmentExpressionBinder.BindAssignmentExpression(assignmentExpression);
      if (syntax is ParenthesizedExpressionSyntax parenthesizedExpression)
        return Session.ExpressionBinder.BindExpression(parenthesizedExpression.Expression, expectedType);
      if (syntax is TupleExpressionSyntax tupleExpression)
        return Session.CallExpressionBinder.BindTupleExpression(tupleExpression, expectedType);
      if (syntax is UnaryExpressionSyntax unaryExpression)
        return Session.OperatorExpressionBinder.BindUnaryExpression(unaryExpression);
      if (syntax is BinaryExpressionSyntax binaryExpression)
        return Session.OperatorExpressionBinder.BindBinaryExpression(binaryExpression);
      if (syntax is IfExpressionSyntax ifExpression)
        return Session.ConditionalBinder.BindIfExpression(ifExpression, expectedType);
      if (syntax is MatchExpressionSyntax matchExpression)
        return Session.ConditionalBinder.BindMatchExpression(matchExpression, expectedType);
      if (syntax is WhileExpressionSyntax whileExpression)
        return Session.LoopBinder.BindWhileExpression(whileExpression);
      if (syntax is LoopExpressionSyntax loopExpression)
        return Session.LoopBinder.BindLoopExpression(loopExpression);
      if (syntax is BlockExpressionSyntax blockExpression)
        return Session.BlockBinder.BindBlockExpression(blockExpression.Block, expectedType);
      if (syntax is StringLiteralExpressionSyntax stringLiteralExpression)
        return Session.OperatorExpressionBinder.BindStringLiteralExpression(stringLiteralExpression);
      if (syntax is IntegerLiteralExpressionSyntax integerLiteralExpression)
        return Session.OperatorExpressionBinder.BindIntegerLiteralExpression(integerLiteralExpression);
      if (syntax is FloatLiteralExpressionSyntax floatLiteralExpression)
        return Session.OperatorExpressionBinder.BindFloatLiteralExpression(floatLiteralExpression);
      if (syntax is CharacterLiteralExpressionSyntax characterLiteralExpression)
        return Session.OperatorExpressionBinder.BindCharacterLiteralExpression(characterLiteralExpression);
      if (syntax is BooleanLiteralExpressionSyntax booleanLiteralExpression)
        return Session.OperatorExpressionBinder.BindBooleanLiteralExpression(booleanLiteralExpression);
      if (syntax is ArrayLiteralExpressionSyntax arrayLiteralExpression)
        return Session.ExpressionBinder.BindArrayLiteralExpression(arrayLiteralExpression, expectedType);
      if (syntax is AggregateInitializerExpressionSyntax aggregateInitializerExpression)
        return Session.AggregateExpressionBinder.BindAggregateInitializerExpression(aggregateInitializerExpression, expectedType);
      if (syntax is GenericTypeExpressionSyntax genericTypeExpression)
        return Session.ExpressionBinder.BindGenericTypeExpression(genericTypeExpression);
      if (syntax is ElementAccessExpressionSyntax elementAccessExpression)
        return Session.AssignmentExpressionBinder.BindElementAccessExpression(elementAccessExpression);
      if (syntax is NameExpressionSyntax nameExpression)
        return Session.NameExpressionBinder.BindNameExpression(nameExpression, expectedType);
      if (syntax is MemberAccessExpressionSyntax memberAccessExpression)
        return Session.MemberAccessBinder.BindMemberAccessExpression(memberAccessExpression, expectedType);
      if (syntax is CallExpressionSyntax callExpression)
        return Session.CallExpressionBinder.BindCallExpression(callExpression, expectedType);
      if (syntax is ExternExpressionSyntax externExpression)
        return Session.ExternResolver.BindExternExpression(externExpression);
      Session.Diagnostics.ReportUnsupportedExpression(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), syntax.GetType().Name);
      return BoundErrorExpression.Instance;
    }
  
    internal BoundExpression BindGenericTypeExpression(GenericTypeExpressionSyntax syntax)
    {
      var target = Session.ExpressionBinder.BindExpression(syntax.Target);
      var definition = Session.NameResolver.GetReferencedSymbol(target) as TypeSymbol;
      if (definition == null)
        return BoundErrorExpression.Instance;
      var actualArity = syntax.TypeArgumentList.Arguments.Count;
      var expectedArity = definition.IsGenericDefinition ? definition.GenericParameters.Count : 0;
      if (!definition.IsGenericDefinition || actualArity != expectedArity)
      {
        Session.Diagnostics.ReportWrongGenericArity(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), definition.Name, expectedArity, actualArity);
        foreach (var argument in syntax.TypeArgumentList.Arguments)
          Session.TypeResolver.BindTypeSyntax(argument);
        return BoundErrorExpression.Instance;
      }
  
      var arguments = Session.TypeResolver.BindTypeArguments(syntax.TypeArgumentList);
      if (Session.TypeResolver.ContainsTypeError(arguments))
        return BoundErrorExpression.Instance;
      var constructed = definition.Construct(arguments);
      return new BoundNameExpression(constructed.Name, constructed, constructed);
    }
  
    internal BoundExpression BindArrayLiteralExpression(ArrayLiteralExpressionSyntax syntax, TypeSymbol expectedType)
    {
      if (syntax.IsRepeat)
        return Session.ExpressionBinder.BindArrayRepeatExpression(syntax, expectedType);
      var expectedElementType = expectedType?.TypeKind == TypeKind.Array ? expectedType.ElementType : null;
      if (expectedType != null && expectedType != TypeSymbol.Error && expectedType.TypeKind != TypeKind.Array)
      {
        Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), expectedType.Name, "array");
        return BoundErrorExpression.Instance;
      }
  
      if (syntax.Elements.Count == 0 && expectedElementType == null)
      {
        Session.Diagnostics.ReportCannotInferArrayType(Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
        return BoundErrorExpression.Instance;
      }
  
      var elements = new List<BoundExpression>(syntax.Elements.Count);
      TypeSymbol elementType = expectedElementType;
      for (var index = 0; index < syntax.Elements.Count; index++)
      {
        var element = Session.ExpressionBinder.BindExpression(syntax.Elements[index], elementType);
        elements.Add(element);
        if (elementType == null && element.Type != TypeSymbol.Error)
        {
          elementType = element.Type;
        }
      }
  
      if (elementType == null)
      {
        Session.Diagnostics.ReportCannotInferArrayType(Session.BinderSyntaxFacts.GetExpressionSpan(syntax));
        return BoundErrorExpression.Instance;
      }
  
      var hasError = false;
      for (var index = 0; index < elements.Count; index++)
      {
        var element = elements[index];
        if (element.Type == TypeSymbol.Error || Session.ConversionClassifier.CanAssignToLocal(elementType, element.Type))
        {
          continue;
        }
  
        Session.Diagnostics.ReportArrayElementTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Elements[index]), elementType.Name, element.Type.Name);
        hasError = true;
      }
  
      var arrayType = expectedType?.TypeKind == TypeKind.Array ? expectedType : Session.ExpressionBinder.BindArrayType(elementType, Session.BinderSyntaxFacts.GetExpressionSpan(syntax), out _);
      if (arrayType == TypeSymbol.Error || hasError)
        return BoundErrorExpression.Instance;
      ArrayIntrinsicSymbols intrinsics = null;
      if (!Session.ExpressionBinder.IsAggregateStorageType(arrayType) && !Session.Environment.ExternCatalog.TryGetArrayIntrinsics(arrayType, out intrinsics, out var reason))
      {
        Session.Diagnostics.ReportArrayTypeNotAvailable(Session.BinderSyntaxFacts.GetExpressionSpan(syntax), arrayType.Name, reason);
        return BoundErrorExpression.Instance;
      }
  
      return new BoundArrayLiteralExpression(elements, arrayType, intrinsics, Session.ExpressionBinder.GetAggregateArrayIntrinsics(arrayType));
    }
  
    internal BoundExpression BindArrayRepeatExpression(ArrayLiteralExpressionSyntax syntax, TypeSymbol expectedType)
    {
      var span = Session.BinderSyntaxFacts.GetExpressionSpan(syntax);
      var hasTypeOperand = Session.ExpressionBinder.TryResolveRepeatTypeOperand(syntax.RepeatOperand, out var typeOperand);
      var hasValueOperand = Session.TypeResolver.CanResolveRepeatValueOperand(syntax.RepeatOperand);
      if (hasTypeOperand && hasValueOperand)
      {
        Session.Diagnostics.ReportAmbiguousArrayRepeatOperand(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.RepeatOperand));
        return BoundErrorExpression.Instance;
      }
  
      if (!hasTypeOperand && !hasValueOperand)
      {
        Session.Diagnostics.ReportUnresolvedArrayRepeatOperand(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.RepeatOperand));
        return BoundErrorExpression.Instance;
      }
  
      BoundExpression operand = null;
      TypeSymbol elementType;
      if (hasTypeOperand)
      {
        elementType = typeOperand;
      }
      else
      {
        var contextualElementType = expectedType?.TypeKind == TypeKind.Array ? expectedType.ElementType : null;
        operand = Session.ExpressionBinder.BindExpression(syntax.RepeatOperand, contextualElementType);
        if (operand.Type == TypeSymbol.Error)
          return BoundErrorExpression.Instance;
        elementType = contextualElementType ?? operand.Type;
        if (!Session.ConversionClassifier.CanAssignToLocal(elementType, operand.Type))
        {
          Session.Diagnostics.ReportArrayElementTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.RepeatOperand), elementType.Name, operand.Type.Name);
          return BoundErrorExpression.Instance;
        }
      }
  
      var arrayType = expectedType?.TypeKind == TypeKind.Array ? expectedType : Session.ExpressionBinder.BindArrayType(elementType, span, out _);
      if (arrayType == TypeSymbol.Error)
        return BoundErrorExpression.Instance;
      ArrayIntrinsicSymbols intrinsics = null;
      if (!Session.ExpressionBinder.IsAggregateStorageType(arrayType) && !Session.Environment.ExternCatalog.TryGetArrayIntrinsics(arrayType, out intrinsics, out var reason))
      {
        Session.Diagnostics.ReportArrayTypeNotAvailable(span, arrayType.Name, reason);
        return BoundErrorExpression.Instance;
      }
  
      var indexType = intrinsics?.IndexType ?? TypeSymbol.I32;
      var length = Session.ExpressionBinder.BindExpression(syntax.RepeatLength, indexType);
      if (length.Type != TypeSymbol.Error && length.Type != indexType)
      {
        Session.Diagnostics.ReportInvalidArrayLengthType(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.RepeatLength), indexType.Name, length.Type.Name);
        return BoundErrorExpression.Instance;
      }
  
      if (Session.TypeResolver.TryGetInt32Constant(length, out var constantLength) && constantLength < 0)
      {
        Session.Diagnostics.ReportNegativeArrayLength(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.RepeatLength), constantLength);
        return BoundErrorExpression.Instance;
      }
  
      BoundBinaryOperator lessThan = null;
      BoundBinaryOperator increment = null;
      if (operand != null)
      {
        lessThan = Session.OperatorResolver.BindBinaryOperator(SyntaxKind.LessToken, indexType, indexType, span, reportDiagnostics: false);
        increment = Session.OperatorResolver.BindBinaryOperator(SyntaxKind.PlusToken, indexType, indexType, span, reportDiagnostics: false);
        if (lessThan == null || increment == null)
        {
          Session.Diagnostics.ReportUnresolvedArrayRepeatOperand(span);
          return BoundErrorExpression.Instance;
        }
      }
  
      return new BoundArrayRepeatExpression(arrayType, operand, length, intrinsics, lessThan, increment, Session.ExpressionBinder.GetAggregateArrayIntrinsics(arrayType));
    }
  
    internal TypeSymbol BindArrayType(TypeSymbol elementType, TextSpan span, out ArrayIntrinsicSymbols intrinsics)
    {
      intrinsics = null;
      if (elementType == null || elementType == TypeSymbol.Error)
        return TypeSymbol.Error;
      var arrayType = TypeSymbol.Array(elementType);
      if (elementType.IsAggregate || elementType.TypeKind == TypeKind.Array && elementType.ElementType?.IsAggregate == true)
      {
        foreach (var leaf in AggregateLayout.GetLeaves(arrayType))
        {
          var leafReason = "aggregate array leaf is not an array ABI type";
          if (leaf.Type.TypeKind != TypeKind.Array || !Session.Environment.ExternCatalog.TryGetArrayIntrinsics(leaf.Type, out _, out leafReason))
          {
            Session.Diagnostics.ReportInvalidAggregateArrayLeafAbi(span, arrayType.Name, leaf.PathText, leaf.Type.Name, leafReason);
            return TypeSymbol.Error;
          }
        }
  
        return arrayType;
      }
  
      if (Session.Environment.ExternCatalog.TryGetArrayIntrinsics(arrayType, out intrinsics, out var reason))
      {
        return arrayType;
      }
  
      Session.Diagnostics.ReportArrayTypeNotAvailable(span, arrayType.Name, reason);
      return TypeSymbol.Error;
    }
  
    internal bool IsAggregateStorageType(TypeSymbol type)
    {
      return type?.IsAggregate == true || type?.TypeKind == TypeKind.Array && type.ElementType?.IsAggregate == true;
    }
  
    internal IReadOnlyList<ArrayIntrinsicSymbols> GetAggregateArrayIntrinsics(TypeSymbol arrayType)
    {
      if (!Session.ExpressionBinder.IsAggregateStorageType(arrayType) || arrayType.TypeKind != TypeKind.Array)
        return null;
      var result = new List<ArrayIntrinsicSymbols>();
      foreach (var leaf in AggregateLayout.GetLeaves(arrayType))
      {
        if (Session.Environment.ExternCatalog.TryGetArrayIntrinsics(leaf.Type, out var intrinsics, out _))
        {
          result.Add(intrinsics);
        }
      }
  
      return result;
    }
  
    internal bool TryResolveRepeatTypeOperand(ExpressionSyntax syntax, out TypeSymbol type)
    {
      type = null;
      if (syntax is NameExpressionSyntax name)
        return Session.TypeResolver.TryResolveTypeNameQuiet(name.Name, name.IdentifierToken.Span, out type);
      if (syntax is MemberAccessExpressionSyntax member && Session.TypeResolver.TryGetQualifiedName(member, out var qualifiedName))
      {
        return Session.TypeResolver.TryResolveTypeNameQuiet(qualifiedName, Session.BinderSyntaxFacts.GetExpressionSpan(member), out type);
      }
  
      if (syntax is ArrayLiteralExpressionSyntax array && !array.IsRepeat && array.Elements.Count == 1 && array.SeparatorTokens.Count == 0 && Session.ExpressionBinder.TryResolveRepeatTypeOperand(array.Elements[0], out var elementType))
      {
        type = TypeSymbol.Array(elementType);
        return true;
      }
  
      return false;
    }
  }
}
