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
    public SyntaxToken OpenBracketToken { get; }
    public TypeSyntax ElementType { get; }
    public SyntaxToken CloseBracketToken { get; }
    public bool IsArray => ElementType != null;

    public TypeSyntax(
        IReadOnlyList<SyntaxToken> parts,
        IReadOnlyList<SyntaxToken> dotTokens)
    {
      Parts = parts;
      DotTokens = dotTokens;
    }

    public TypeSyntax(
        SyntaxToken openBracketToken,
        TypeSyntax elementType,
        SyntaxToken closeBracketToken)
    {
      OpenBracketToken = openBracketToken;
      ElementType = elementType;
      CloseBracketToken = closeBracketToken;
      Parts = new List<SyntaxToken>();
      DotTokens = new List<SyntaxToken>();
    }

    public string GetText()
    {
      if (IsArray)
        return $"[{ElementType.GetText()}]";

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
      if (IsArray)
      {
        return TextSpan.FromBounds(
            OpenBracketToken.Span.Start,
            CloseBracketToken.Span.End);
      }

      if (Parts.Count == 0)
        return new TextSpan(0, 0);

      return TextSpan.FromBounds(Parts[0].Span.Start, Parts[^1].Span.End);
    }
  }
}
