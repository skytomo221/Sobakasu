using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuCommentTests
    {
        [Test]
        public void Lexer_SkipsLineComments()
        {
            AssertLexesLike(
                "// comment\nlet x = 1;",
                "let x = 1;");
            AssertLexesLike(
                "let x = 1; // comment\nlet y = 2;",
                "let x = 1; let y = 2;");
            AssertLexesLike(
                "let x = 1;\n// comment without trailing newline",
                "let x = 1;");
        }

        [Test]
        public void Lexer_SkipsInlineAndMultilineBlockComments()
        {
            AssertLexesLike(
                "/* comment */ let x = 1;",
                "let x = 1;");
            AssertLexesLike(
                "/*\ncomment\ncomment\n*/\nlet x = 1;",
                "let x = 1;");
            AssertLexesLike(
                "let x = /* ignored */ 1;",
                "let x = 1;");
        }

        [Test]
        public void Lexer_SkipsNestedBlockComments()
        {
            AssertLexesLike(
                @"/*
outer
/*
middle
/* inner */
middle again
*/
outer again
*/
let x = 1;",
                "let x = 1;");
        }

        [Test]
        public void Parser_DoesNotParseCommentContents()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"/*
fn broken( {
???
*/
on interact() {
  let x = /* ignored */ 1;
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);
            Assert.That(syntax.Members, Has.Count.EqualTo(1));
            Assert.That(syntax.Members[0], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Lexer_PreservesCommentMarkersInsideStrings()
        {
            var tokens = LexAll(
                @"let a = ""//"";
let b = ""/*"";
let c = ""*/"";
let d = ""/* foo */"";",
                out var diagnostics);
            var stringValues = new List<string>();
            foreach (var token in tokens)
            {
                if (token.Kind == SyntaxKind.String)
                    stringValues.Add((string)token.Value);
            }

            Assert.That(diagnostics.Diagnostics, Is.Empty);
            Assert.That(
                stringValues,
                Is.EqualTo(new[] { "//", "/*", "*/", "/* foo */" }));
        }

        [Test]
        public void Lexer_PreservesDivisionAndDivisionAssignment()
        {
            var tokens = LexAll("10 / 2; value /= 2;", out var diagnostics);

            Assert.That(diagnostics.Diagnostics, Is.Empty);
            Assert.That(tokens[1].Kind, Is.EqualTo(SyntaxKind.SlashToken));
            Assert.That(tokens[5].Kind, Is.EqualTo(SyntaxKind.SlashEqualsToken));
        }

        [Test]
        public void Lexer_TreatsBlockCommentAsTokenSeparator()
        {
            AssertLexesLike(
                "let/* comment */x = 1;",
                "let x = 1;");
        }

        [TestCase("/*\nunterminated")]
        [TestCase("/*\nouter\n/*\ninner\n*/")]
        public void Lexer_ReportsUnterminatedBlockCommentAtOutermostOpener(string source)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));
            var token = lexer.Lex();

            Assert.That(token.Kind, Is.EqualTo(SyntaxKind.EndOfFile));
            Assert.That(lexer.Diagnostics.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(lexer.Diagnostics.Diagnostics[0].Code, Is.EqualTo("SBK0008"));
            Assert.That(lexer.Diagnostics.Diagnostics[0].Message,
                Is.EqualTo("Unterminated block comment."));
            Assert.That(lexer.Diagnostics.Diagnostics[0].Span.Start, Is.EqualTo(0));
            Assert.That(lexer.Diagnostics.Diagnostics[0].Span.Length, Is.EqualTo(2));
        }

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void Parser_KeepsDiagnosticLocationAfterMultilineBlockComment(string newLine)
        {
            var source = string.Join(newLine, new[]
            {
                "on interact() {",
                "}",
                "",
                "/*",
                "line 1",
                "line 2",
                "*/",
                "this_is_invalid"
            });
            var text = SourceText.From(source);
            var parser = new SobakasuParser(text);
            parser.ParseCompilationUnit();
            var diagnostic = FindDiagnostic(parser.Diagnostics.Diagnostics, "SBK1002");
            var line = text.GetLineFromPosition(diagnostic.Span.Start);

            Assert.That(diagnostic.Span.Start,
                Is.EqualTo(source.LastIndexOf("this_is_invalid")));
            Assert.That(line, Is.SameAs(text.Lines[7]));
            Assert.That(diagnostic.Span.Start - line.Start + 1, Is.EqualTo(1));
        }

        [Test]
        public void Compiler_CompilesCommentsWithoutChangingProgramSemantics()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"/// ordinary line comment
/**
  ordinary block comment
  /* nested comment */
*/
on interact() {
  let quotient = 10 / 2; // division remains an operator
  extern UnityEngine.Debug.Log(""/* not a comment */"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(".export _interact"));
        }

        [Test]
        public void Compiler_FailsForUnterminatedBlockComment()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "on interact() {}\n/* never closed");

            Assert.That(result.Success, Is.False);
            Assert.That(ContainsDiagnostic(result.Diagnostics, "SBK0008"), Is.True,
                result.ErrorText);
            Assert.That(result.ErrorText, Does.Contain("Unterminated block comment."));
            Assert.That(result.ErrorText, Does.Contain("line 2, col 1"));
        }

        private static void AssertLexesLike(string source, string sourceWithoutComments)
        {
            var actual = LexAll(source, out var actualDiagnostics);
            var expected = LexAll(sourceWithoutComments, out var expectedDiagnostics);

            Assert.That(actualDiagnostics.Diagnostics, Is.Empty);
            Assert.That(expectedDiagnostics.Diagnostics, Is.Empty);
            Assert.That(actual, Has.Count.EqualTo(expected.Count));

            for (var index = 0; index < actual.Count; index++)
            {
                Assert.That(actual[index].Kind, Is.EqualTo(expected[index].Kind),
                    $"Token kind mismatch at index {index}.");
                Assert.That(actual[index].Text, Is.EqualTo(expected[index].Text),
                    $"Token text mismatch at index {index}.");
                Assert.That(actual[index].Value, Is.EqualTo(expected[index].Value),
                    $"Token value mismatch at index {index}.");
            }
        }

        private static List<SyntaxToken> LexAll(
            string source,
            out DiagnosticBag diagnostics)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));
            var tokens = new List<SyntaxToken>();
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                tokens.Add(token);
            }
            while (token.Kind != SyntaxKind.EndOfFile);

            diagnostics = lexer.Diagnostics;
            return tokens;
        }

        private static Diagnostic FindDiagnostic(
            IReadOnlyList<Diagnostic> diagnostics,
            string code)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == code)
                    return diagnostic;
            }

            Assert.Fail($"Expected diagnostic {code}.");
            return default;
        }

        private static bool ContainsDiagnostic(
            IReadOnlyList<Diagnostic> diagnostics,
            string code)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }
    }
}
