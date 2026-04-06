using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class UseDirectiveSyntax : MemberSyntax
  {
    public SyntaxToken UseKeyword { get; }
    public QualifiedNameSyntax Path { get; }
    public SyntaxToken AsKeyword { get; }
    public SyntaxToken Alias { get; }
    public SyntaxToken SemicolonToken { get; }
    public bool IsMalformed { get; }

    public UseDirectiveSyntax(
        SyntaxToken useKeyword,
        QualifiedNameSyntax path,
        SyntaxToken asKeyword,
        SyntaxToken alias,
        SyntaxToken semicolonToken,
        bool isMalformed)
    {
      UseKeyword = useKeyword;
      Path = path;
      AsKeyword = asKeyword;
      Alias = alias;
      SemicolonToken = semicolonToken;
      IsMalformed = isMalformed;
    }
  }
}
