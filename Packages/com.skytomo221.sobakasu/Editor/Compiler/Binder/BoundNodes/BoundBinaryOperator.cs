using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
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
}
