using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BinaryExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Left { get; }
    public Token OperatorToken { get; }
    public ExpressionSyntax Right { get; }

    public BinaryExpressionSyntax(ExpressionSyntax left, Token op, ExpressionSyntax right, TextSpan span)
        : base(span)
    {
      Left = left;
      OperatorToken = op;
      Right = right;
    }
  }
}
