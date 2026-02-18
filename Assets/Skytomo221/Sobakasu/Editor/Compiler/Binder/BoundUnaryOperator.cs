using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundUnaryOperator
  {
    public TokenKind SyntaxKind { get; }
    public BoundUnaryOperatorKind Kind { get; }
    public TypeSymbol OperandType { get; }
    public TypeSymbol ResultType { get; }

    private BoundUnaryOperator(TokenKind syntaxKind, BoundUnaryOperatorKind kind, TypeSymbol operandType, TypeSymbol resultType)
    {
      SyntaxKind = syntaxKind;
      Kind = kind;
      OperandType = operandType;
      ResultType = resultType;
    }

    private static readonly BoundUnaryOperator[] _operators =
    {
            new BoundUnaryOperator(TokenKind.Plus,  BoundUnaryOperatorKind.Identity,       TypeSymbol.Int,   TypeSymbol.Int),
            new BoundUnaryOperator(TokenKind.Plus,  BoundUnaryOperatorKind.Identity,       TypeSymbol.Float, TypeSymbol.Float),
            new BoundUnaryOperator(TokenKind.Minus, BoundUnaryOperatorKind.Negation,       TypeSymbol.Int,   TypeSymbol.Int),
            new BoundUnaryOperator(TokenKind.Minus, BoundUnaryOperatorKind.Negation,       TypeSymbol.Float, TypeSymbol.Float),
            new BoundUnaryOperator(TokenKind.Bang,  BoundUnaryOperatorKind.LogicalNegation,TypeSymbol.Bool,  TypeSymbol.Bool),
        };

    public static BoundUnaryOperator? Bind(TokenKind kind, TypeSymbol operandType)
    {
      foreach (var op in _operators)
        if (op.SyntaxKind == kind && ReferenceEquals(op.OperandType, operandType))
          return op;
      return null;
    }
  }
}
