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
    public SyntaxToken QuestionToken { get; }
    public string MemberName =>
        (Name.Text ?? string.Empty) +
        (QuestionToken == null ? string.Empty : "?");

    public MemberAccessExpressionSyntax(
        ExpressionSyntax expression,
        SyntaxToken dotToken,
        SyntaxToken name,
        SyntaxToken questionToken = null)
    {
      Expression = expression;
      DotToken = dotToken;
      Name = name;
      QuestionToken = questionToken;
    }
  }
}
