using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class ArrayLiteralExpressionSyntax : ExpressionSyntax
  {
    public SyntaxToken OpenBracketToken { get; }
    public IReadOnlyList<ExpressionSyntax> Elements { get; }
    public IReadOnlyList<SyntaxToken> SeparatorTokens { get; }
    public SyntaxToken CloseBracketToken { get; }

    public ArrayLiteralExpressionSyntax(
        SyntaxToken openBracketToken,
        IReadOnlyList<ExpressionSyntax> elements,
        IReadOnlyList<SyntaxToken> separatorTokens,
        SyntaxToken closeBracketToken)
    {
      OpenBracketToken = openBracketToken;
      Elements = elements;
      SeparatorTokens = separatorTokens;
      CloseBracketToken = closeBracketToken;
    }
  }
}
