using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class IdentifierSyntaxTests
    {
        [Test]
        public void IdentifierFacts_RecognizeUnicodeXidBoundaries()
        {
            Assert.That(SobakasuIdentifierFacts.IsNormalIdentifier("ascii_2"), Is.True);
            Assert.That(SobakasuIdentifierFacts.IsNormalIdentifier("東京"), Is.True);
            Assert.That(SobakasuIdentifierFacts.IsNormalIdentifier("a\u0301"), Is.True);
            Assert.That(SobakasuIdentifierFacts.IsNormalIdentifier("\u0301a"), Is.False);
            Assert.That(SobakasuIdentifierFacts.IsNormalIdentifier("🙂"), Is.False);
            Assert.That(SobakasuIdentifierFacts.IsNormalIdentifier("a-b"), Is.False);
            Assert.That(SobakasuIdentifierFacts.MangleUasmSymbol("a-b"),
                Is.EqualTo("__sbk_q_612D62"));
            Assert.That(SobakasuIdentifierFacts.MangleUasmSymbol("🙂"),
                Is.EqualTo("__sbk_q_F09F9982"));
        }

        [Test]
        public void IdentifierFacts_QuoteReservedBareIdentifiersWhenRendering()
        {
            Assert.That(SobakasuIdentifierFacts.IsBareIdentifier("normal"), Is.True);
            Assert.That(SobakasuIdentifierFacts.IsBareIdentifier("loop"), Is.False);
            Assert.That(SobakasuIdentifierFacts.IsBareIdentifier("type"), Is.False);
            Assert.That(SobakasuIdentifierFacts.IsBareIdentifier("null"), Is.False);

            Assert.That(SobakasuIdentifierFacts.TryRenderIdentifier("normal", out var normal),
                Is.True);
            Assert.That(normal, Is.EqualTo("normal"));
            Assert.That(SobakasuIdentifierFacts.TryRenderIdentifier("loop", out var loop),
                Is.True);
            Assert.That(loop, Is.EqualTo("`loop`"));
            Assert.That(SobakasuIdentifierFacts.TryRenderIdentifier("type", out var type),
                Is.True);
            Assert.That(type, Is.EqualTo("`type`"));
            Assert.That(SobakasuIdentifierFacts.TryRenderIdentifier("null", out var @null),
                Is.True);
            Assert.That(@null, Is.EqualTo("`null`"));
        }

        [Test]
        public void Lexer_UnquotesQuotedIdentifiersAndReportsUnterminatedForms()
        {
            var lexer = new SobakasuLexer(SourceText.From(
                "`if` `type` `loop` `a-b` `first name` `🙂`"));
            var names = new List<string>();
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                if (token.Kind == SyntaxKind.Identifier)
                    names.Add(token.Text);
            }
            while (token.Kind != SyntaxKind.EndOfFile);

            Assert.That(names, Is.EqualTo(new[]
            {
                "if", "type", "loop", "a-b", "first name", "🙂"
            }));
            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty);

            var unterminated = new SobakasuLexer(SourceText.From("`unfinished"));
            unterminated.Lex();
            Assert.That(ContainsCode(unterminated.Diagnostics.Diagnostics, "SBK0009"), Is.True);
        }

        [Test]
        public void Compiler_ResolvesQuotedNamesWithoutSourceSpelling()
        {
            const string source = @"struct `日本語の型` { `a-b`: i32, }
fn `loop`(`if`: i32) -> i32 {
  let `first name` = `if`;
  `first name`
}
on start {
  let `🙂` = `loop`(1);
  let value = `日本語の型` { `a-b`: `🙂`, };
  let copied = value.`a-b`;
}";

            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);

            var result = SobakasuCompiler.CompileToUasm(source);
            Assert.That(result.Success, Is.True, result.ErrorText);
        }

        [Test]
        public void Parser_ParsesQuotedCallableNamesAndExternSelectors()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"pub impl Example = extern Example {
  pub fn `null`?(self) -> bool = extern self.`loop`
  pub fn `type`(self) = extern self.`type`()
}
on start {
  foo.`loop`;
  foo.`type`;
}"));

            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);
            var implementation = syntax.Members[0] as ImplDeclarationSyntax;
            Assert.That(implementation, Is.Not.Null);
            Assert.That(implementation.Methods[0].Name, Is.EqualTo("null?"));
            Assert.That(implementation.Methods[1].Name, Is.EqualTo("type"));
        }

        [Test]
        public void Compiler_RejectsUnrepresentableQuotedNetworkReceiverName()
        {
            var result = SobakasuCompiler.CompileToUasm(
                "receive `a-b`() {} on start {}");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorText,
                Does.Contain("Public Udon symbol 'a-b' cannot be represented"));
        }

        private static bool ContainsCode(
            IReadOnlyList<Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic> diagnostics,
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
