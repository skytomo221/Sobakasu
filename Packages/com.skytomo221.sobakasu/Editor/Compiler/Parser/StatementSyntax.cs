using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    abstract class StatementSyntax : SyntaxNode
    {
    }

    sealed class SendStatementSyntax : StatementSyntax
    {
        public SyntaxToken SendKeyword { get; }
        public SyntaxToken ReceiverName { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<ExpressionSyntax> Arguments { get; }
        public IReadOnlyList<SyntaxToken> ArgumentSeparators { get; }
        public SyntaxToken CloseParenToken { get; }
        public SyntaxToken ToKeyword { get; }
        public ExpressionSyntax Target { get; }
        public SyntaxToken SemicolonToken { get; }

        public SendStatementSyntax(
            SyntaxToken sendKeyword,
            SyntaxToken receiverName,
            SyntaxToken openParenToken,
            IReadOnlyList<ExpressionSyntax> arguments,
            IReadOnlyList<SyntaxToken> argumentSeparators,
            SyntaxToken closeParenToken,
            SyntaxToken toKeyword,
            ExpressionSyntax target,
            SyntaxToken semicolonToken)
        {
            SendKeyword = sendKeyword;
            ReceiverName = receiverName;
            OpenParenToken = openParenToken;
            Arguments = arguments;
            ArgumentSeparators = argumentSeparators;
            CloseParenToken = closeParenToken;
            ToKeyword = toKeyword;
            Target = target;
            SemicolonToken = semicolonToken;
        }
    }

    sealed class TypeClauseSyntax : SyntaxNode
    {
        public SyntaxToken ColonToken { get; }
        public TypeSyntax Type { get; }

        public TypeClauseSyntax(
            SyntaxToken colonToken,
            TypeSyntax type)
        {
            ColonToken = colonToken;
            Type = type;
        }
    }

    abstract class BindingPatternSyntax : SyntaxNode
    {
    }

    sealed class NameBindingPatternSyntax : BindingPatternSyntax
    {
        public SyntaxToken Identifier { get; }
        public bool IsDiscard => Identifier.Text == "_";

        public NameBindingPatternSyntax(SyntaxToken identifier)
        {
            Identifier = identifier;
        }
    }

    sealed class TupleBindingPatternSyntax : BindingPatternSyntax
    {
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<BindingPatternSyntax> Elements { get; }
        public IReadOnlyList<SyntaxToken> Separators { get; }
        public SyntaxToken CloseParenToken { get; }

        public TupleBindingPatternSyntax(
            SyntaxToken openParenToken,
            IReadOnlyList<BindingPatternSyntax> elements,
            IReadOnlyList<SyntaxToken> separators,
            SyntaxToken closeParenToken)
        {
            OpenParenToken = openParenToken;
            Elements = elements;
            Separators = separators;
            CloseParenToken = closeParenToken;
        }
    }

    sealed class VariableDeclarationStatementSyntax : StatementSyntax
    {
        public SyntaxToken LetKeyword { get; }
        public SyntaxToken MutKeyword { get; }
        public BindingPatternSyntax Pattern { get; }
        public SyntaxToken Identifier =>
            (Pattern as NameBindingPatternSyntax)?.Identifier;
        public TypeClauseSyntax TypeClause { get; }
        public SyntaxToken EqualsToken { get; }
        public ExpressionSyntax Initializer { get; }
        public SyntaxToken SemicolonToken { get; }

        public VariableDeclarationStatementSyntax(
            SyntaxToken letKeyword,
            SyntaxToken mutKeyword,
            BindingPatternSyntax pattern,
            TypeClauseSyntax typeClause,
            SyntaxToken equalsToken,
            ExpressionSyntax initializer,
            SyntaxToken semicolonToken)
        {
            LetKeyword = letKeyword;
            MutKeyword = mutKeyword;
            Pattern = pattern;
            TypeClause = typeClause;
            EqualsToken = equalsToken;
            Initializer = initializer;
            SemicolonToken = semicolonToken;
        }
    }

    sealed class InvalidLocalDeclarationStatementSyntax : StatementSyntax
    {
        public SyntaxToken Keyword { get; }
        public SyntaxToken SemicolonToken { get; }

        public InvalidLocalDeclarationStatementSyntax(
            SyntaxToken keyword,
            SyntaxToken semicolonToken)
        {
            Keyword = keyword;
            SemicolonToken = semicolonToken;
        }
    }

    sealed class ReturnStatementSyntax : StatementSyntax
    {
        public SyntaxToken ReturnKeyword { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken SemicolonToken { get; }

        public ReturnStatementSyntax(
            SyntaxToken returnKeyword,
            ExpressionSyntax expression,
            SyntaxToken semicolonToken)
        {
            ReturnKeyword = returnKeyword;
            Expression = expression;
            SemicolonToken = semicolonToken;
        }
    }

    sealed class BreakStatementSyntax : StatementSyntax
    {
        public SyntaxToken BreakKeyword { get; }
        public SyntaxToken Label { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken SemicolonToken { get; }

        public BreakStatementSyntax(
            SyntaxToken breakKeyword,
            SyntaxToken label,
            ExpressionSyntax expression,
            SyntaxToken semicolonToken)
        {
            BreakKeyword = breakKeyword;
            Label = label;
            Expression = expression;
            SemicolonToken = semicolonToken;
        }
    }

    sealed class ContinueStatementSyntax : StatementSyntax
    {
        public SyntaxToken ContinueKeyword { get; }
        public SyntaxToken Label { get; }
        public SyntaxToken SemicolonToken { get; }

        public ContinueStatementSyntax(
            SyntaxToken continueKeyword,
            SyntaxToken label,
            SyntaxToken semicolonToken)
        {
            ContinueKeyword = continueKeyword;
            Label = label;
            SemicolonToken = semicolonToken;
        }
    }

    sealed class RedoStatementSyntax : StatementSyntax
    {
        public SyntaxToken RedoKeyword { get; }
        public SyntaxToken Label { get; }
        public SyntaxToken SemicolonToken { get; }

        public RedoStatementSyntax(
            SyntaxToken redoKeyword,
            SyntaxToken label,
            SyntaxToken semicolonToken)
        {
            RedoKeyword = redoKeyword;
            Label = label;
            SemicolonToken = semicolonToken;
        }
    }
}
