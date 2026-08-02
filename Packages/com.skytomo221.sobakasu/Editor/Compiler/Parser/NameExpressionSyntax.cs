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
    public SyntaxToken QuestionToken { get; }
    public string Name =>
        (IdentifierToken.Text ?? string.Empty) +
        (QuestionToken == null ? string.Empty : "?");

    public NameExpressionSyntax(
        SyntaxToken identifierToken,
        SyntaxToken questionToken = null)
    {
      IdentifierToken = identifierToken;
      QuestionToken = questionToken;
    }
  }
}
