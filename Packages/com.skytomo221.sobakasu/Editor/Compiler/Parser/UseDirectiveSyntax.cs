using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class UseDirectiveSyntax : MemberSyntax
  {
    public SyntaxToken PubKeyword { get; }
    public SyntaxToken UseKeyword { get; }
    public QualifiedNameSyntax Path { get; }
    public SyntaxToken AsKeyword { get; }
    public SyntaxToken Alias { get; }
    public SyntaxToken SemicolonToken { get; }
    public bool IsMalformed { get; }
    public bool IsReExport => PubKeyword != null;

    public UseDirectiveSyntax(
        SyntaxToken pubKeyword,
        SyntaxToken useKeyword,
        QualifiedNameSyntax path,
        SyntaxToken asKeyword,
        SyntaxToken alias,
        SyntaxToken semicolonToken,
        bool isMalformed)
    {
      PubKeyword = pubKeyword;
      UseKeyword = useKeyword;
      Path = path;
      AsKeyword = asKeyword;
      Alias = alias;
      SemicolonToken = semicolonToken;
      IsMalformed = isMalformed;
    }
  }

  sealed class ModDeclarationSyntax : MemberSyntax
  {
    public SyntaxToken PubKeyword { get; }
    public SyntaxToken ModKeyword { get; }
    public SyntaxToken Identifier { get; }
    public SyntaxToken SemicolonToken { get; }
    public bool IsMalformed { get; }
    public bool IsPublic => PubKeyword != null;

    public ModDeclarationSyntax(
        SyntaxToken pubKeyword,
        SyntaxToken modKeyword,
        SyntaxToken identifier,
        SyntaxToken semicolonToken,
        bool isMalformed)
    {
      PubKeyword = pubKeyword;
      ModKeyword = modKeyword;
      Identifier = identifier;
      SemicolonToken = semicolonToken;
      IsMalformed = isMalformed;
    }
  }
}
