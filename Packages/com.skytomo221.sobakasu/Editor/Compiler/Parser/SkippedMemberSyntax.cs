using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  public sealed class SkippedMemberSyntax : MemberSyntax
  {
    public SyntaxToken BadToken { get; }

    public SkippedMemberSyntax(SyntaxToken badToken)
    {
      BadToken = badToken;
    }
  }
}
