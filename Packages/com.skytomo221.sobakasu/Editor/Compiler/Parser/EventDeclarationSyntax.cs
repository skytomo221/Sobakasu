using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class EventDeclarationSyntax : MemberSyntax
  {
    public SyntaxToken OnKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken OpenParenToken { get; }
    public SyntaxToken CloseParenToken { get; }
    public BlockStatementSyntax Body { get; }

    public EventDeclarationSyntax(
        SyntaxToken onKeyword,
        SyntaxToken identifier,
        SyntaxToken openParenToken,
        SyntaxToken closeParenToken,
        BlockStatementSyntax body)
    {
      OnKeyword = onKeyword;
      Identifier = identifier;
      OpenParenToken = openParenToken;
      CloseParenToken = closeParenToken;
      Body = body;
    }
  }
}
