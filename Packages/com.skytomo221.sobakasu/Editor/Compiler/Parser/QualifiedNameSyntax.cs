using System.Collections.Generic;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class QualifiedNameSyntax : SyntaxNode
  {
    public IReadOnlyList<SyntaxToken> Identifiers { get; }
    public IReadOnlyList<SyntaxToken> DotTokens { get; }

    public QualifiedNameSyntax(
        IReadOnlyList<SyntaxToken> identifiers,
        IReadOnlyList<SyntaxToken> dotTokens)
    {
      Identifiers = identifiers;
      DotTokens = dotTokens;
    }

    public string GetText()
    {
      var builder = new StringBuilder();
      for (var index = 0; index < Identifiers.Count; index++)
      {
        if (index > 0)
          builder.Append('.');

        builder.Append(Identifiers[index].Text);
      }

      return builder.ToString();
    }
  }
}
