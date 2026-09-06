using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal abstract class ParserComponent
    {
        protected ParserComponent(ParserState state) => State = state;

        protected ParserState State { get; }
        protected DiagnosticBag Diagnostics => State.Diagnostics;
        protected SyntaxToken Current => State.Current;
        protected int Position { get => State.Position; set => State.Position = value; }
        protected int SuppressAggregateInitializerDepth
        {
            get => State.SuppressAggregateInitializerDepth;
            set => State.SuppressAggregateInitializerDepth = value;
        }

        protected SyntaxToken Peek(int offset) => State.Peek(offset);
        protected SyntaxToken NextToken() => State.NextToken();
        protected SyntaxToken MatchToken(SyntaxKind kind) => State.MatchToken(kind);
        protected SyntaxToken MatchTypeArgumentGreaterToken() =>
            State.MatchTypeArgumentGreaterToken();
    }
}
