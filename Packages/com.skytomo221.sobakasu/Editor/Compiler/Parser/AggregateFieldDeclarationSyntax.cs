using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class AggregateFieldDeclarationSyntax : SyntaxNode
    {
        public SyntaxToken Identifier { get; }
        public SyntaxToken ColonToken { get; }
        public TypeSyntax Type { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public SyntaxToken ExternalMemberName { get; }
        public SyntaxToken CommaToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public AggregateFieldDeclarationSyntax(
            SyntaxToken identifier,
            SyntaxToken colonToken,
            TypeSyntax type,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            SyntaxToken externalMemberName,
            SyntaxToken commaToken)
        {
            Identifier = identifier;
            ColonToken = colonToken;
            Type = type;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalMemberName = externalMemberName;
            CommaToken = commaToken;
        }
    }
}
