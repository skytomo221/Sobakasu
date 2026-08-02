namespace Skytomo221.Sobakasu.Compiler.Parser
{
  public abstract class MemberSyntax : SyntaxNode
  {
  }

  internal enum SynchronizationModeSyntaxKind
  {
    None,
    Linear,
    Smooth,
    Invalid
  }

  internal sealed class SynchronizationModifierSyntax : SyntaxNode
  {
    public Syntax.SyntaxToken SyncKeyword { get; }
    public Syntax.SyntaxToken OpenParenToken { get; }
    public Syntax.SyntaxToken ModeToken { get; }
    public Syntax.SyntaxToken CloseParenToken { get; }
    public SynchronizationModeSyntaxKind Mode { get; }

    public SynchronizationModifierSyntax(
        Syntax.SyntaxToken syncKeyword,
        Syntax.SyntaxToken openParenToken,
        Syntax.SyntaxToken modeToken,
        Syntax.SyntaxToken closeParenToken,
        SynchronizationModeSyntaxKind mode)
    {
      SyncKeyword = syncKeyword;
      OpenParenToken = openParenToken;
      ModeToken = modeToken;
      CloseParenToken = closeParenToken;
      Mode = mode;
    }
  }

  internal sealed class StateDeclarationSyntax : MemberSyntax
  {
    public Syntax.SyntaxToken PubKeyword { get; }
    public SynchronizationModifierSyntax SynchronizationModifier { get; }
    public Syntax.SyntaxToken LetKeyword { get; }
    public Syntax.SyntaxToken MutKeyword { get; }
    public Syntax.SyntaxToken Identifier { get; }
    public TypeClauseSyntax TypeClause { get; }
    public Syntax.SyntaxToken EqualsToken { get; }
    public ExpressionSyntax Initializer { get; }
    public Syntax.SyntaxToken SemicolonToken { get; }

    public StateDeclarationSyntax(
        Syntax.SyntaxToken pubKeyword,
        SynchronizationModifierSyntax synchronizationModifier,
        Syntax.SyntaxToken letKeyword,
        Syntax.SyntaxToken mutKeyword,
        Syntax.SyntaxToken identifier,
        TypeClauseSyntax typeClause,
        Syntax.SyntaxToken equalsToken,
        ExpressionSyntax initializer,
        Syntax.SyntaxToken semicolonToken)
    {
      PubKeyword = pubKeyword;
      SynchronizationModifier = synchronizationModifier;
      LetKeyword = letKeyword;
      MutKeyword = mutKeyword;
      Identifier = identifier;
      TypeClause = typeClause;
      EqualsToken = equalsToken;
      Initializer = initializer;
      SemicolonToken = semicolonToken;
    }
  }
}
