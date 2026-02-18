using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class UnaryExpressionSyntax : ExpressionSyntax
  {
    public Token OperatorToken { get; }
    public ExpressionSyntax Operand { get; }

    public UnaryExpressionSyntax(Token op, ExpressionSyntax operand, TextSpan span)
        : base(span)
    {
      OperatorToken = op;
      Operand = operand;
    }
  }
}
