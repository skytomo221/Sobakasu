using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundBinaryOperator
  {
    public TokenKind SyntaxKind { get; }
    public BoundBinaryOperatorKind Kind { get; }
    public TypeSymbol LeftType { get; }
    public TypeSymbol RightType { get; }
    public TypeSymbol ResultType { get; }

    private BoundBinaryOperator(TokenKind syntaxKind, BoundBinaryOperatorKind kind,
                                TypeSymbol leftType, TypeSymbol rightType, TypeSymbol resultType)
    {
      SyntaxKind = syntaxKind;
      Kind = kind;
      LeftType = leftType;
      RightType = rightType;
      ResultType = resultType;
    }

    private static readonly BoundBinaryOperator[] _operators =
    {
            // int arithmetic
            new BoundBinaryOperator(TokenKind.Plus,  BoundBinaryOperatorKind.Add,      TypeSymbol.Int, TypeSymbol.Int, TypeSymbol.Int),
            new BoundBinaryOperator(TokenKind.Minus, BoundBinaryOperatorKind.Subtract, TypeSymbol.Int, TypeSymbol.Int, TypeSymbol.Int),
            new BoundBinaryOperator(TokenKind.Star,  BoundBinaryOperatorKind.Multiply, TypeSymbol.Int, TypeSymbol.Int, TypeSymbol.Int),
            new BoundBinaryOperator(TokenKind.Slash, BoundBinaryOperatorKind.Divide,   TypeSymbol.Int, TypeSymbol.Int, TypeSymbol.Int),

            // float arithmetic
            new BoundBinaryOperator(TokenKind.Plus,  BoundBinaryOperatorKind.Add,      TypeSymbol.Float, TypeSymbol.Float, TypeSymbol.Float),
            new BoundBinaryOperator(TokenKind.Minus, BoundBinaryOperatorKind.Subtract, TypeSymbol.Float, TypeSymbol.Float, TypeSymbol.Float),
            new BoundBinaryOperator(TokenKind.Star,  BoundBinaryOperatorKind.Multiply, TypeSymbol.Float, TypeSymbol.Float, TypeSymbol.Float),
            new BoundBinaryOperator(TokenKind.Slash, BoundBinaryOperatorKind.Divide,   TypeSymbol.Float, TypeSymbol.Float, TypeSymbol.Float),

            // string concat
            new BoundBinaryOperator(TokenKind.Plus,  BoundBinaryOperatorKind.Add,      TypeSymbol.String, TypeSymbol.String, TypeSymbol.String),

            // equality
            new BoundBinaryOperator(TokenKind.EqualEqual, BoundBinaryOperatorKind.Equals,    TypeSymbol.Int,    TypeSymbol.Int,    TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.BangEqual,  BoundBinaryOperatorKind.NotEquals, TypeSymbol.Int,    TypeSymbol.Int,    TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.EqualEqual, BoundBinaryOperatorKind.Equals,    TypeSymbol.Float,  TypeSymbol.Float,  TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.BangEqual,  BoundBinaryOperatorKind.NotEquals, TypeSymbol.Float,  TypeSymbol.Float,  TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.EqualEqual, BoundBinaryOperatorKind.Equals,    TypeSymbol.Bool,   TypeSymbol.Bool,   TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.BangEqual,  BoundBinaryOperatorKind.NotEquals, TypeSymbol.Bool,   TypeSymbol.Bool,   TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.EqualEqual, BoundBinaryOperatorKind.Equals,    TypeSymbol.String, TypeSymbol.String, TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.BangEqual,  BoundBinaryOperatorKind.NotEquals, TypeSymbol.String, TypeSymbol.String, TypeSymbol.Bool),

            // comparisons
            new BoundBinaryOperator(TokenKind.Less,         BoundBinaryOperatorKind.Less,         TypeSymbol.Int,   TypeSymbol.Int,   TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.LessEqual,    BoundBinaryOperatorKind.LessOrEqual,  TypeSymbol.Int,   TypeSymbol.Int,   TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.Greater,      BoundBinaryOperatorKind.Greater,      TypeSymbol.Int,   TypeSymbol.Int,   TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.GreaterEqual, BoundBinaryOperatorKind.GreaterOrEqual,TypeSymbol.Int,  TypeSymbol.Int,   TypeSymbol.Bool),

            new BoundBinaryOperator(TokenKind.Less,         BoundBinaryOperatorKind.Less,         TypeSymbol.Float, TypeSymbol.Float, TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.LessEqual,    BoundBinaryOperatorKind.LessOrEqual,  TypeSymbol.Float, TypeSymbol.Float, TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.Greater,      BoundBinaryOperatorKind.Greater,      TypeSymbol.Float, TypeSymbol.Float, TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.GreaterEqual, BoundBinaryOperatorKind.GreaterOrEqual,TypeSymbol.Float,TypeSymbol.Float, TypeSymbol.Bool),

            // logical
            new BoundBinaryOperator(TokenKind.AndAnd, BoundBinaryOperatorKind.LogicalAnd, TypeSymbol.Bool, TypeSymbol.Bool, TypeSymbol.Bool),
            new BoundBinaryOperator(TokenKind.OrOr,   BoundBinaryOperatorKind.LogicalOr,  TypeSymbol.Bool, TypeSymbol.Bool, TypeSymbol.Bool),
        };

    public static BoundBinaryOperator? Bind(TokenKind kind, TypeSymbol left, TypeSymbol right)
    {
      foreach (var op in _operators)
        if (op.SyntaxKind == kind && ReferenceEquals(op.LeftType, left) && ReferenceEquals(op.RightType, right))
          return op;
      return null;
    }
  }
}
