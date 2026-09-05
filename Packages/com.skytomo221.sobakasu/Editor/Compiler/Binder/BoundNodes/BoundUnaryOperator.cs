using System;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
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
}
