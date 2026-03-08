using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class CallExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Target { get; }
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<ExpressionSyntax> Arguments { get; }
    public SyntaxToken CloseParenToken { get; }

    public CallExpressionSyntax(
        ExpressionSyntax target,
        SyntaxToken openParenToken,
        IReadOnlyList<ExpressionSyntax> arguments,
        SyntaxToken closeParenToken)
    {
      Target = target;
      OpenParenToken = openParenToken;
      Arguments = arguments;
      CloseParenToken = closeParenToken;
    }
  }
}
