using Skytomo221.Sobakasu.Compiler.Text;
using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
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
}
