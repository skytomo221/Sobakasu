using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    internal sealed class ParserState
    {
        private readonly SyntaxToken[] _tokens;
        private int _position;
        private SyntaxToken _pendingGreaterToken;

        internal int SuppressAggregateInitializerDepth { get; set; }
        internal DiagnosticBag Diagnostics { get; } = new();
        internal ParserUtilities Utilities { get; set; }
        internal ModuleParser Modules { get; set; }
        internal ExpressionParser Expressions { get; set; }
        internal PatternParser Patterns { get; set; }
        internal TypeParser Types { get; set; }
        internal StatementParser Statements { get; set; }
        internal DeclarationParser Declarations { get; set; }

        // Component aliases keep cross-component calls explicit while state
        // ownership remains centralized here.
        internal ParserUtilities ParserUtilities => Utilities;
        internal ModuleParser ModuleParser => Modules;
        internal ExpressionParser ExpressionParser => Expressions;
        internal PatternParser PatternParser => Patterns;
        internal TypeParser TypeParser => Types;
        internal StatementParser StatementParser => Statements;
        internal DeclarationParser DeclarationParser => Declarations;

        internal ParserState(SourceText text, string sourcePath)
        {
            var lexer = new SobakasuLexer(text);
            lexer.Diagnostics.SourcePath = sourcePath ?? string.Empty;
            Diagnostics.SourcePath = sourcePath ?? string.Empty;
            var tokens = new List<SyntaxToken>();
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                if (token.Kind != SyntaxKind.BadToken)
                    tokens.Add(token);
            }
            while (token.Kind != SyntaxKind.EndOfFile);

            _tokens = tokens.ToArray();
            Diagnostics.AddRange(lexer.Diagnostics);
        }

        internal int Position { get => _position; set => _position = value; }
        internal SyntaxToken Current => _pendingGreaterToken ?? Peek(0);

        internal SyntaxToken Peek(int offset)
        {
            var index = _position + offset;
            if (index >= _tokens.Length)
                return _tokens[^1];

            return _tokens[index];
        }

        internal SyntaxToken NextToken()
        {
            var current = Current;
            if (_pendingGreaterToken != null)
            {
                _pendingGreaterToken = null;
                return current;
            }

            _position++;
            return current;
        }

        internal SyntaxToken MatchTypeArgumentGreaterToken()
        {
            if (Current.Kind == SyntaxKind.GreaterToken)
                return NextToken();

            if (Current.Kind == SyntaxKind.GreaterGreaterToken)
            {
                var shift = NextToken();
                var first = new SyntaxToken(
                    SyntaxKind.GreaterToken,
                    new TextSpan(shift.Span.Start, 1),
                    ">");
                _pendingGreaterToken = new SyntaxToken(
                    SyntaxKind.GreaterToken,
                    new TextSpan(shift.Span.Start + 1, 1),
                    ">");
                return first;
            }

            return MatchToken(SyntaxKind.GreaterToken);
        }

        internal SyntaxToken MatchToken(SyntaxKind kind)
        {
            if (Current.Kind == kind)
                return NextToken();

            Diagnostics.ReportUnexpectedToken(Current.Span, Current.Kind, kind);
            return new SyntaxToken(kind, Current.Span, string.Empty);
        }
    }

}
