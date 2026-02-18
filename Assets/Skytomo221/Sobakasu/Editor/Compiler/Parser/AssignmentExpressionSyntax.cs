using System;
using System.Collections.Generic;

using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class AssignmentExpressionSyntax : ExpressionSyntax
  {
    public Token Identifier { get; }
    public Token EqualToken { get; }
    public ExpressionSyntax Expression { get; }

    public AssignmentExpressionSyntax(Token id, Token eq, ExpressionSyntax expr, TextSpan span)
        : base(span)
    {
      Identifier = id;
      EqualToken = eq;
      Expression = expr;
    }
  }
}
