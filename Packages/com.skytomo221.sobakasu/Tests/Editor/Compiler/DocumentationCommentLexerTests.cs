using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class DocumentationCommentLexerTests
    {
        [TestCase("/// Foo\n///\n///  Indented", "Foo\n\n Indented")]
        [TestCase("    /// Foo\n    /// Bar", "Foo\nBar")]
        [TestCase("/// Foo\r\n/// Bar", "Foo\nBar")]
        [TestCase("///  Preserved\n///\tTab", " Preserved\n\tTab")]
        public void Lexer_RecognizesAndNormalizesDocumentationBlocks(
            string source,
            string expectedMarkdown)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));
            var documentation = lexer.Lex();

            Assert.That(documentation.Kind,
                Is.EqualTo(SyntaxKind.DocumentationComment));
            Assert.That(documentation.Text, Is.EqualTo(expectedMarkdown));
            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty);
        }

        [TestCase("// ordinary\nfunction foo() {}")]
        [TestCase("//// ordinary\nfunction foo() {}")]
        [TestCase("//! ordinary\nfunction foo() {}")]
        [TestCase("/** ordinary */\nfunction foo() {}")]
        public void Lexer_TreatsUnsupportedDocumentationFormsAsOrdinaryComments(
            string source)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));

            Assert.That(lexer.Lex().Kind, Is.EqualTo(SyntaxKind.FunctionKeyword));
            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty);
        }
    }
}
