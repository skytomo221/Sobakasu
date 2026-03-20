using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  public sealed class CompilationUnitSyntax : SyntaxNode
  {
    public IReadOnlyList<MemberSyntax> Members { get; }
    public SyntaxToken EndOfFileToken { get; }

    public CompilationUnitSyntax(
        IReadOnlyList<MemberSyntax> members,
        SyntaxToken endOfFileToken)
    {
      Members = members;
      EndOfFileToken = endOfFileToken;
    }
  }
}
