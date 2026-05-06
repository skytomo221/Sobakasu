using System.Collections.Generic;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class TypeSyntax : SyntaxNode
  {
    public IReadOnlyList<SyntaxToken> Parts { get; }
    public IReadOnlyList<SyntaxToken> DotTokens { get; }

    public TypeSyntax(
        IReadOnlyList<SyntaxToken> parts,
        IReadOnlyList<SyntaxToken> dotTokens)
    {
      Parts = parts;
      DotTokens = dotTokens;
    }

    public string GetText()
    {
      var builder = new StringBuilder();
      for (var index = 0; index < Parts.Count; index++)
      {
        if (index > 0)
          builder.Append('.');

        builder.Append(Parts[index].Text);
      }

      return builder.ToString();
    }

    public TextSpan GetSpan()
    {
      if (Parts.Count == 0)
        return new TextSpan(0, 0);

      return TextSpan.FromBounds(Parts[0].Span.Start, Parts[^1].Span.End);
    }
  }
}
