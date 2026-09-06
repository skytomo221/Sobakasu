using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
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
}
