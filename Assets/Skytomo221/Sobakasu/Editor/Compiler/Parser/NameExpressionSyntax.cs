using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class NameExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken IdentifierToken { get; }

    public NameExpressionSyntax(SyntaxToken identifierToken)
    {
      IdentifierToken = identifierToken;
    }
  }
}
