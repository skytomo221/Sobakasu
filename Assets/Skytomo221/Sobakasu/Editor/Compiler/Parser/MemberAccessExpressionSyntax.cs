using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class MemberAccessExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Expression { get; }
    public SyntaxToken DotToken { get; }
    public SyntaxToken Name { get; }

    public MemberAccessExpressionSyntax(
        ExpressionSyntax expression,
        SyntaxToken dotToken,
        SyntaxToken name)
    {
      Expression = expression;
      DotToken = dotToken;
      Name = name;
    }
  }
}
