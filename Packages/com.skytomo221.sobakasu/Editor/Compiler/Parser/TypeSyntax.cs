using System.Collections.Generic;
using System.Text;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
  sealed class GenericParameterListSyntax : SyntaxNode
  {
    public SyntaxToken LessToken { get; }
    public IReadOnlyList<SyntaxToken> Parameters { get; }
    public IReadOnlyList<SyntaxToken> Separators { get; }
    public SyntaxToken GreaterToken { get; }

    public GenericParameterListSyntax(
        SyntaxToken lessToken,
        IReadOnlyList<SyntaxToken> parameters,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken greaterToken)
    {
      LessToken = lessToken;
      Parameters = parameters;
      Separators = separators;
      GreaterToken = greaterToken;
    }
  }

  sealed class TypeArgumentListSyntax : SyntaxNode
  {
    public SyntaxToken LessToken { get; }
    public IReadOnlyList<TypeSyntax> Arguments { get; }
    public IReadOnlyList<SyntaxToken> Separators { get; }
    public SyntaxToken GreaterToken { get; }

    public TypeArgumentListSyntax(
        SyntaxToken lessToken,
        IReadOnlyList<TypeSyntax> arguments,
        IReadOnlyList<SyntaxToken> separators,
        SyntaxToken greaterToken)
    {
      LessToken = lessToken;
      Arguments = arguments;
      Separators = separators;
      GreaterToken = greaterToken;
    }

    public string GetText()
    {
      var builder = new StringBuilder();
      builder.Append('<');
      for (var index = 0; index < Arguments.Count; index++)
      {
        if (index > 0)
          builder.Append(", ");
        builder.Append(Arguments[index].GetText());
      }
      builder.Append('>');
      return builder.ToString();
    }
  }

  sealed class TypeSyntax : SyntaxNode
  {
    public IReadOnlyList<SyntaxToken> Parts { get; }
    public IReadOnlyList<SyntaxToken> DotTokens { get; }
    public SyntaxToken OpenBracketToken { get; }
    public TypeSyntax ElementType { get; }
    public SyntaxToken CloseBracketToken { get; }
    public TypeArgumentListSyntax TypeArgumentList { get; }
    public SyntaxToken OpenParenToken { get; }
    public IReadOnlyList<TypeSyntax> TupleElementTypes { get; }
    public IReadOnlyList<SyntaxToken> TupleSeparators { get; }
    public SyntaxToken CloseParenToken { get; }
    public bool IsArray => ElementType != null;
    public bool IsTuple => OpenParenToken != null;

    public TypeSyntax(
        IReadOnlyList<SyntaxToken> parts,
        IReadOnlyList<SyntaxToken> dotTokens,
        TypeArgumentListSyntax typeArgumentList = null)
    {
      Parts = parts;
      DotTokens = dotTokens;
      TypeArgumentList = typeArgumentList;
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

    public TypeSyntax(
        SyntaxToken openParenToken,
        IReadOnlyList<TypeSyntax> tupleElementTypes,
        IReadOnlyList<SyntaxToken> tupleSeparators,
        SyntaxToken closeParenToken)
    {
      OpenParenToken = openParenToken;
      TupleElementTypes = tupleElementTypes;
      TupleSeparators = tupleSeparators;
      CloseParenToken = closeParenToken;
      Parts = new List<SyntaxToken>();
      DotTokens = new List<SyntaxToken>();
    }

    public string GetNameText()
    {
      if (IsTuple)
        return GetText();

      if (IsArray)
        return GetText();

      var builder = new StringBuilder();
      for (var index = 0; index < Parts.Count; index++)
      {
        if (index > 0)
          builder.Append('.');
        builder.Append(Parts[index].Text);
      }
      return builder.ToString();
    }

    public string GetText()
    {
      if (IsTuple)
      {
        var tupleBuilder = new StringBuilder();
        tupleBuilder.Append('(');
        for (var index = 0; index < TupleElementTypes.Count; index++)
        {
          if (index > 0)
            tupleBuilder.Append(", ");
          tupleBuilder.Append(TupleElementTypes[index].GetText());
        }
        if (TupleElementTypes.Count == 1)
          tupleBuilder.Append(',');
        tupleBuilder.Append(')');
        return tupleBuilder.ToString();
      }

      if (IsArray)
        return $"[{ElementType.GetText()}]";

      var builder = new StringBuilder(GetNameText());
      if (TypeArgumentList != null)
        builder.Append(TypeArgumentList.GetText());
      return builder.ToString();
    }

    public TextSpan GetSpan()
    {
      if (IsTuple)
      {
        return TextSpan.FromBounds(
            OpenParenToken.Span.Start,
            CloseParenToken.Span.End);
      }

      if (IsArray)
      {
        return TextSpan.FromBounds(
            OpenBracketToken.Span.Start,
            CloseBracketToken.Span.End);
      }

      if (Parts.Count == 0)
        return new TextSpan(0, 0);

      return TextSpan.FromBounds(
          Parts[0].Span.Start,
          TypeArgumentList?.GreaterToken.Span.End ?? Parts[^1].Span.End);
    }
  }

  sealed class GenericTypeExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Target { get; }
    public TypeArgumentListSyntax TypeArgumentList { get; }

    public GenericTypeExpressionSyntax(
        ExpressionSyntax target,
        TypeArgumentListSyntax typeArgumentList)
    {
      Target = target;
      TypeArgumentList = typeArgumentList;
    }
  }
}
