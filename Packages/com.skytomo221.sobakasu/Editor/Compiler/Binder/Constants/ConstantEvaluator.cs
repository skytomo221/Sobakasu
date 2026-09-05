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
  internal sealed class ConstantEvaluator : BinderComponent
  {
    private Dictionary<ParameterSymbol, object> _constantParameters;
    private readonly HashSet<FunctionSymbol> _evaluatingFunctions = new();

    internal ConstantEvaluator(BindingSession session) : base(session)
    {
    }
  
    internal bool TryEvaluateStateConstant(BoundExpression expression, TypeSymbol expectedType, out object value)
    {
      value = null;
      if (expression is BoundNameExpression parameterExpression &&
          parameterExpression.Symbol is ParameterSymbol parameter &&
          _constantParameters != null &&
          Session.ConversionClassifier.CanAssignToLocal(expectedType, parameter.Type))
        return _constantParameters.TryGetValue(parameter, out value);

      if (expression is BoundUserFunctionCallExpression functionCall)
        return TryEvaluateDeclarativeOperator(functionCall, expectedType, out value);

      if (expression is BoundCallExpression call && call.ConstantEvaluationExpression != null)
        return TryEvaluateStateConstant(call.ConstantEvaluationExpression, expectedType, out value);

      if (expression is BoundLiteralExpression literal)
      {
        if (!Session.ConversionClassifier.CanAssignToLocal(expectedType, literal.Type))
          return false;
        value = literal.Value;
        return true;
      }
  
      if (expression is BoundNameExpression nameExpression && nameExpression.Symbol is ConstantSymbol constant && constant.HasConstantValue && Session.ConversionClassifier.CanAssignToLocal(expectedType, constant.Type))
      {
        value = constant.ConstantValue;
        return true;
      }
  
      if (expression is BoundStructConstructionExpression structConstruction)
        return Session.ConstantEvaluator.TryEvaluateStructConstant(structConstruction, expectedType, out value);
      if (expression is BoundTupleExpression tupleExpression)
        return Session.ConstantEvaluator.TryEvaluateTupleConstant(tupleExpression, expectedType, out value);
      if (expression is BoundEnumConstructionExpression enumConstruction)
        return Session.ConstantEvaluator.TryEvaluateEnumConstant(enumConstruction, expectedType, out value);
      if (expression is BoundArrayLiteralExpression arrayLiteral)
      {
        if (Session.ExpressionBinder.IsAggregateStorageType(expectedType))
        {
          return Session.ConstantEvaluator.TryEvaluateAggregateArrayConstant(arrayLiteral.Elements, expectedType, out value);
        }
  
        if (expectedType?.TypeKind != TypeKind.Array || arrayLiteral.Type != expectedType || !Session.Environment.ExternCatalog.TryGetClrType(expectedType.ElementType, out var elementClrType))
        {
          return false;
        }
  
        var array = Array.CreateInstance(elementClrType, arrayLiteral.Elements.Count);
        for (var index = 0; index < arrayLiteral.Elements.Count; index++)
        {
          if (!Session.ConstantEvaluator.TryEvaluateStateConstant(arrayLiteral.Elements[index], expectedType.ElementType, out var element))
          {
            return false;
          }
  
          array.SetValue(element, index);
        }
  
        value = array;
        return true;
      }
  
      if (expression is BoundArrayRepeatExpression arrayRepeat)
      {
        var repeatIndexType = arrayRepeat.Intrinsics?.IndexType ?? TypeSymbol.I32;
        if (expectedType?.TypeKind != TypeKind.Array || arrayRepeat.Type != expectedType || !Session.ConstantEvaluator.TryEvaluateStateConstant(arrayRepeat.Length, repeatIndexType, out var lengthValue) || lengthValue is not int length || length < 0)
        {
          return false;
        }
  
        if (Session.ExpressionBinder.IsAggregateStorageType(expectedType))
        {
          return Session.ConstantEvaluator.TryEvaluateAggregateArrayRepeatConstant(arrayRepeat, expectedType, length, out value);
        }
  
        if (!Session.Environment.ExternCatalog.TryGetClrType(expectedType.ElementType, out var elementClrType))
        {
          return false;
        }
  
        var array = Array.CreateInstance(elementClrType, length);
        if (!arrayRepeat.UsesDefaultValue)
        {
          for (var index = 0; index < length; index++)
          {
            if (!Session.ConstantEvaluator.TryEvaluateStateConstant(arrayRepeat.Operand, expectedType.ElementType, out var element))
            {
              return false;
            }
  
            array.SetValue(element, index);
          }
        }
  
        value = array;
        return true;
      }
  
      if (expression is BoundBinaryExpression binary)
      {
        if (!Session.ConstantEvaluator.TryEvaluateStateConstant(binary.Left, binary.Operator.LeftType, out var left) || !Session.ConstantEvaluator.TryEvaluateStateConstant(binary.Right, binary.Operator.RightType, out var right))
        {
          return false;
        }
  
        try
        {
          value = Session.ConstantEvaluator.EvaluateBinaryConstant(binary.Operator.Kind, left, right);
          return value != null && Session.ConversionClassifier.CanAssignToLocal(expectedType, binary.Type);
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
  
      if (expression is not BoundUnaryExpression unary || !Session.ConstantEvaluator.TryEvaluateStateConstant(unary.Operand, unary.Operator.OperandType, out var operand))
      {
        return false;
      }
  
      try
      {
        switch (unary.Operator.Kind)
        {
          case BoundUnaryOperatorKind.Identity:
            value = operand;
            return Session.ConversionClassifier.CanAssignToLocal(expectedType, unary.Type);
          case BoundUnaryOperatorKind.LogicalNegation when operand is bool boolean:
            value = !boolean;
            return Session.ConversionClassifier.CanAssignToLocal(expectedType, unary.Type);
          case BoundUnaryOperatorKind.Negation:
            value = Session.ConstantEvaluator.NegateConstant(operand);
            return value != null && Session.ConversionClassifier.CanAssignToLocal(expectedType, unary.Type);
          case BoundUnaryOperatorKind.OnesComplement:
            value = Session.ConstantEvaluator.ComplementConstant(operand);
            return value != null && Session.ConversionClassifier.CanAssignToLocal(expectedType, unary.Type);
        }
      }
      catch (OverflowException)
      {
        return false;
      }
  
      return false;
    }
  
    private bool TryEvaluateDeclarativeOperator(
        BoundUserFunctionCallExpression call,
        TypeSymbol expectedType,
        out object value)
    {
      value = null;
      var function = call.Function;
      if (!Session.ConversionClassifier.CanAssignToLocal(expectedType, call.Type) ||
          !Session.Callables.ExternalBindingExpressions.TryGetValue(function, out var binding) ||
          binding is not BoundCallExpression externalCall ||
          externalCall.ConstantEvaluationExpression == null ||
          _evaluatingFunctions.Contains(function))
        return false;

      // Evaluate only the ABI operation selected by the declaration. Ordinary
      // user functions and arbitrary extern calls are not compile-time code.
      var parameters = new Dictionary<ParameterSymbol, object>();
      if (function.SelfParameter != null)
      {
        if (!TryEvaluateStateConstant(call.Receiver, function.SelfParameter.Type, out var receiver))
          return false;
        parameters.Add(function.SelfParameter, receiver);
      }
      for (var index = 0; index < function.Parameters.Count; index++)
      {
        var parameter = function.Parameters[index];
        if (!TryEvaluateStateConstant(call.Arguments[index], parameter.Type, out var argument))
          return false;
        parameters.Add(parameter, argument);
      }

      var previousParameters = _constantParameters;
      _constantParameters = parameters;
      _evaluatingFunctions.Add(function);
      try
      {
        return TryEvaluateStateConstant(binding, expectedType, out value);
      }
      finally
      {
        _evaluatingFunctions.Remove(function);
        _constantParameters = previousParameters;
      }
    }

    internal object EvaluateBinaryConstant(BoundBinaryOperatorKind kind, object left, object right)
    {
      switch (kind)
      {
        case BoundBinaryOperatorKind.Equals:
          return Equals(left, right);
        case BoundBinaryOperatorKind.NotEquals:
          return !Equals(left, right);
        case BoundBinaryOperatorKind.LogicalAnd:
          return left is bool leftAnd && right is bool rightAnd ? leftAnd && rightAnd : null;
        case BoundBinaryOperatorKind.LogicalOr:
          return left is bool leftOr && right is bool rightOr ? leftOr || rightOr : null;
        case BoundBinaryOperatorKind.Less:
          return Session.ConstantEvaluator.CompareConstants(left, right) < 0;
        case BoundBinaryOperatorKind.LessOrEquals:
          return Session.ConstantEvaluator.CompareConstants(left, right) <= 0;
        case BoundBinaryOperatorKind.Greater:
          return Session.ConstantEvaluator.CompareConstants(left, right) > 0;
        case BoundBinaryOperatorKind.GreaterOrEquals:
          return Session.ConstantEvaluator.CompareConstants(left, right) >= 0;
      }
  
      return left switch
      {
        sbyte value => Session.ConstantEvaluator.EvaluateInt8Constant(kind, value, (sbyte)right),
        byte value => Session.ConstantEvaluator.EvaluateUInt8Constant(kind, value, (byte)right),
        short value => Session.ConstantEvaluator.EvaluateInt16Constant(kind, value, (short)right),
        ushort value => Session.ConstantEvaluator.EvaluateUInt16Constant(kind, value, (ushort)right),
        int value => Session.ConstantEvaluator.EvaluateInt32Constant(kind, value, (int)right),
        uint value => Session.ConstantEvaluator.EvaluateUInt32Constant(kind, value, (uint)right),
        long value => Session.ConstantEvaluator.EvaluateInt64Constant(kind, value, (long)right),
        ulong value => Session.ConstantEvaluator.EvaluateUInt64Constant(kind, value, (ulong)right),
        float value => Session.ConstantEvaluator.EvaluateFloat32Constant(kind, value, (float)right),
        double value => Session.ConstantEvaluator.EvaluateFloat64Constant(kind, value, (double)right),
        string value when kind == BoundBinaryOperatorKind.Addition => value + (string)right,
        _ => null
      };
    }
  
    internal int CompareConstants(object left, object right)
    {
      if (left is IComparable comparable && left.GetType() == right?.GetType())
        return comparable.CompareTo(right);
      throw new ArithmeticException("State constant operands are not comparable.");
    }
  
    internal object EvaluateInt8Constant(BoundBinaryOperatorKind kind, sbyte left, sbyte right)
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
  
    internal object EvaluateUInt8Constant(BoundBinaryOperatorKind kind, byte left, byte right)
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
  
    internal object EvaluateInt16Constant(BoundBinaryOperatorKind kind, short left, short right)
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
  
    internal object EvaluateUInt16Constant(BoundBinaryOperatorKind kind, ushort left, ushort right)
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
  
    internal object EvaluateInt32Constant(BoundBinaryOperatorKind kind, int left, int right)
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
  
    internal object EvaluateUInt32Constant(BoundBinaryOperatorKind kind, uint left, uint right)
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
  
    internal object EvaluateInt64Constant(BoundBinaryOperatorKind kind, long left, long right)
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
  
    internal object EvaluateUInt64Constant(BoundBinaryOperatorKind kind, ulong left, ulong right)
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
  
    internal object EvaluateFloat32Constant(BoundBinaryOperatorKind kind, float left, float right)
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
  
    internal object EvaluateFloat64Constant(BoundBinaryOperatorKind kind, double left, double right)
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
  
    internal object NegateConstant(object value)
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
  
    internal object ComplementConstant(object value)
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
  
    internal bool TryEvaluateStructConstant(BoundStructConstructionExpression expression, TypeSymbol expectedType, out object value)
    {
      value = null;
      if (expression.Type != expectedType)
        return false;
      var leaves = AggregateLayout.GetLeaves(expression.Type);
      var values = new object[leaves.Count];
      foreach (var initializer in expression.Initializers)
      {
        if (!Session.ConstantEvaluator.TryEvaluateStateConstant(initializer.Expression, initializer.Field.Type, out var fieldValue) || !Session.ConstantEvaluator.TryExpandAggregateConstant(initializer.Field.Type, fieldValue, out var fieldLeaves))
        {
          return false;
        }
  
        var indices = AggregateLayout.GetFieldLeafIndices(expression.Type, initializer.Field);
        for (var index = 0; index < indices.Count && index < fieldLeaves.Count; index++)
          values[indices[index]] = fieldLeaves[index];
      }
  
      value = new AggregateConstantValue(expression.Type, values);
      return true;
    }
  
    internal bool TryEvaluateTupleConstant(BoundTupleExpression expression, TypeSymbol expectedType, out object value)
    {
      value = null;
      if (expression.Type != expectedType)
        return false;
      var leaves = AggregateLayout.GetLeaves(expression.Type);
      var values = new object[leaves.Count];
      for (var elementIndex = 0; elementIndex < expression.Elements.Count; elementIndex++)
      {
        var field = expression.Type.AggregateFields[elementIndex];
        if (!Session.ConstantEvaluator.TryEvaluateStateConstant(expression.Elements[elementIndex], field.Type, out var elementValue) || !Session.ConstantEvaluator.TryExpandAggregateConstant(field.Type, elementValue, out var elementLeaves))
        {
          return false;
        }
  
        var indices = AggregateLayout.GetFieldLeafIndices(expression.Type, field);
        for (var leafIndex = 0; leafIndex < indices.Count && leafIndex < elementLeaves.Count; leafIndex++)
        {
          values[indices[leafIndex]] = elementLeaves[leafIndex];
        }
      }
  
      value = new AggregateConstantValue(expression.Type, values);
      return true;
    }
  
    internal bool TryEvaluateEnumConstant(BoundEnumConstructionExpression expression, TypeSymbol expectedType, out object value)
    {
      value = null;
      if (expression.Type != expectedType)
        return false;
      var descriptors = AggregateLayout.GetLeaves(expression.Type);
      var values = new object[descriptors.Count];
      for (var index = 0; index < descriptors.Count; index++)
      {
        if (descriptors[index].IsEnumTag)
          values[index] = expression.Variant.Tag;
      }
  
      foreach (var initializer in expression.Initializers)
      {
        if (!Session.ConstantEvaluator.TryEvaluateStateConstant(initializer.Expression, initializer.Field.Type, out var fieldValue) || !Session.ConstantEvaluator.TryExpandAggregateConstant(initializer.Field.Type, fieldValue, out var fieldLeaves))
        {
          return false;
        }
  
        var leafIndex = 0;
        for (var index = 0; index < descriptors.Count; index++)
        {
          var path = descriptors[index].Path;
          if (path.Count < 2 || !string.Equals(path[0], expression.Variant.Name, StringComparison.Ordinal) || !string.Equals(path[1], initializer.Field.Name, StringComparison.Ordinal))
          {
            continue;
          }
  
          if (leafIndex < fieldLeaves.Count)
            values[index] = fieldLeaves[leafIndex++];
        }
      }
  
      value = new AggregateConstantValue(expression.Type, values);
      return true;
    }
  
    internal bool TryEvaluateAggregateArrayConstant(IReadOnlyList<BoundExpression> elements, TypeSymbol arrayType, out object value)
    {
      var elementValues = new List<AggregateConstantValue>(elements.Count);
      foreach (var element in elements)
      {
        if (!Session.ConstantEvaluator.TryEvaluateStateConstant(element, arrayType.ElementType, out var elementValue) || elementValue is not AggregateConstantValue aggregateElement)
        {
          value = null;
          return false;
        }
  
        elementValues.Add(aggregateElement);
      }
  
      return Session.ConstantEvaluator.TryBuildAggregateArrayConstant(arrayType, elements.Count, index => elementValues[index], out value);
    }
  
    internal bool TryEvaluateAggregateArrayRepeatConstant(BoundArrayRepeatExpression expression, TypeSymbol arrayType, int length, out object value)
    {
      if (expression.UsesDefaultValue)
      {
        return Session.ConstantEvaluator.TryBuildAggregateArrayConstant(arrayType, length, _ => null, out value);
      }
  
      var elements = new AggregateConstantValue[length];
      for (var index = 0; index < length; index++)
      {
        if (!Session.ConstantEvaluator.TryEvaluateStateConstant(expression.Operand, arrayType.ElementType, out var elementValue) || elementValue is not AggregateConstantValue aggregateElement)
        {
          value = null;
          return false;
        }
  
        elements[index] = aggregateElement;
      }
  
      return Session.ConstantEvaluator.TryBuildAggregateArrayConstant(arrayType, length, index => elements[index], out value);
    }
  
    internal bool TryBuildAggregateArrayConstant(TypeSymbol arrayType, int length, Func<int, AggregateConstantValue> getElement, out object value)
    {
      var physicalLeaves = AggregateLayout.GetLeaves(arrayType);
      var leafArrays = new object[physicalLeaves.Count];
      for (var leafIndex = 0; leafIndex < physicalLeaves.Count; leafIndex++)
      {
        var leafType = physicalLeaves[leafIndex].Type;
        if (leafType.TypeKind != TypeKind.Array || !Session.Environment.ExternCatalog.TryGetClrType(leafType.ElementType, out var elementClrType))
        {
          value = null;
          return false;
        }
  
        var array = Array.CreateInstance(elementClrType, length);
        for (var index = 0; index < length; index++)
        {
          var element = getElement(index);
          if (element != null && leafIndex < element.Leaves.Count)
            array.SetValue(element.Leaves[leafIndex], index);
        }
  
        leafArrays[leafIndex] = array;
      }
  
      value = new AggregateConstantValue(arrayType, leafArrays);
      return true;
    }
  
    internal bool TryExpandAggregateConstant(TypeSymbol type, object value, out IReadOnlyList<object> leaves)
    {
      if (Session.ExpressionBinder.IsAggregateStorageType(type))
      {
        if (value is AggregateConstantValue aggregate && aggregate.Type == type)
        {
          leaves = aggregate.Leaves;
          return true;
        }
  
        leaves = null;
        return false;
      }
  
      leaves = new[]
      {
        value
      };
      return true;
    }
  }
}
