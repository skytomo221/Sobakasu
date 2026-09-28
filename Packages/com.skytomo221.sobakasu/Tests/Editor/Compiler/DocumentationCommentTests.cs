using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class DocumentationCommentTests
    {
        [Test]
        public void Lexer_RecognizesAndNormalizesDocumentationBlocks()
        {
            var lexer = new SobakasuLexer(SourceText.From("    /// Foo\n    ///\n    ///  Indented\nfn foo() {}"));
            var documentation = lexer.Lex();

            Assert.That(documentation.Kind, Is.EqualTo(SyntaxKind.DocumentationComment));
            Assert.That(documentation.Text, Is.EqualTo("Foo\n\n Indented"));
            Assert.That(lexer.Lex().Kind, Is.EqualTo(SyntaxKind.FnKeyword));
        }

        [TestCase("// ordinary\nfn foo() {}")]
        [TestCase("//// ordinary\nfn foo() {}")]
        [TestCase("//! ordinary\nfn foo() {}")]
        [TestCase("/** ordinary */\nfn foo() {}")]
        public void Lexer_TreatsUnsupportedDocumentationFormsAsOrdinaryComments(string source)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));

            Assert.That(lexer.Lex().Kind, Is.EqualTo(SyntaxKind.FnKeyword));
        }

        [Test]
        public void Parser_AttachesOnlyAnImmediatelyFollowingDocumentableDeclaration()
        {
            var parser = new SobakasuParser(SourceText.From(@"/// Orphan.

/// Attached.
pub fn foo() {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(HasCode(parser.Diagnostics.Diagnostics, "SBK1051"), Is.True);
            var function = (FunctionDeclarationSyntax)syntax.Members[0];
            Assert.That(function.Documentation.Markdown, Is.EqualTo("Attached."));
        }

        [TestCase("/// Orphan.\n// ordinary\nfn foo() {}")]
        [TestCase("/// Orphan.\nuse foo;")]
        [TestCase("/// Orphan.\nimpl f32 {}")]
        [TestCase("fn foo() {\n    /// Orphan.\n    let x = 1;\n}")]
        public void Parser_ReportsOrphanDocumentationWithoutUnexpectedTokenDiagnostics(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(HasCode(parser.Diagnostics.Diagnostics, "SBK1051"), Is.True);
            Assert.That(HasCode(parser.Diagnostics.Diagnostics, "SBK1002"), Is.False);
        }

        [Test]
        public void Parser_AttachesDocumentationToAggregateMembersAndImplMethods()
        {
            var parser = new SobakasuParser(SourceText.From(@"/// Struct.
struct S {
    /// Field.
    value: f32,
}
/// Enum.
enum E {
    /// Variant.
    A,
}
impl S {
    /// Method.
    fn method() {}
}"));
            var syntax = parser.ParseCompilationUnit();

            var structure = (StructDeclarationSyntax)syntax.Members[0];
            var enumeration = (EnumDeclarationSyntax)syntax.Members[1];
            var implementation = (ImplDeclarationSyntax)syntax.Members[2];
            Assert.That(structure.Documentation.Markdown, Is.EqualTo("Struct."));
            Assert.That(structure.Fields[0].Documentation.Markdown, Is.EqualTo("Field."));
            Assert.That(enumeration.Documentation.Markdown, Is.EqualTo("Enum."));
            Assert.That(enumeration.Variants[0].Documentation.Markdown, Is.EqualTo("Variant."));
            Assert.That(implementation, Is.Not.InstanceOf<IDocumentableSyntax>());
            Assert.That(implementation.Methods[0].Documentation.Markdown, Is.EqualTo("Method."));
        }

        [Test]
        public void Binder_PreservesDocumentationOnBoundSymbols()
        {
            var parser = new SobakasuParser(SourceText.From(@"/// Constant.
const value: i32 = 1;
/// State.
pub sync state speed: f32;
/// Function.
fn foo() {}
/// Event.
on interact() {}
/// Receiver.
receive ping() {}"));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.HasErrors, Is.False);

            var binder = new SobakasuBinder();
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.HasErrors, Is.False, Format(binder.Diagnostics.Diagnostics));
            Assert.That(program.Constants[0].ConstantSymbol.Documentation.Markdown, Is.EqualTo("Constant."));
            Assert.That(program.States[0].StateSymbol.Documentation.Markdown, Is.EqualTo("State."));
            Assert.That(program.Functions[0].FunctionSymbol.Documentation.Markdown, Is.EqualTo("Function."));
            Assert.That(program.Events[0].EventSymbol.Documentation.Markdown, Is.EqualTo("Event."));
            Assert.That(program.NetworkReceivers[0].ReceiveSymbol.Documentation.Markdown, Is.EqualTo("Receiver."));
        }

        private static bool HasCode(IReadOnlyList<Diagnostic> diagnostics, string code)
        {
            foreach (var diagnostic in diagnostics)
                if (diagnostic.Code == code)
                    return true;
            return false;
        }

        private static string Format(IReadOnlyList<Diagnostic> diagnostics)
        {
            var messages = new List<string>();
            foreach (var diagnostic in diagnostics)
                messages.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", messages);
        }
    }
}
