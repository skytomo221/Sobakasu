using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class DocumentationCommentParserTests
    {
        [Test]
        public void Parser_AttachesDocumentationToEveryDocumentableDeclaration()
        {
            const string source = @"/// Type.
type Alias = extern System.String;
/// Struct.
struct Struct {
    /// Field.
    value: i32,
}
/// Enum.
enum Enum {
    /// Variant.
    Named {
        /// Payload field.
        value: i32,
    },
}
/// Function.
fn function() {}
/// Extern function.
fn external() = extern Test.Api.External()
impl Struct {
    /// Receiver method.
    fn receiver(self) {}
    /// Associated function.
    fn associated() {}
}
/// Constant.
const constant: i32 = 1;
state {
    /// Private state.
    private_state: i32 = 0;
    /// Public state.
    pub public_state: i32 = field;
    /// Sync state.
    pub sync sync_state: i32 = field;
}
behavior {
    /// Event.
    on start {}
    /// Receive.
    receive ping() {}
}";
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            AssertDocumentation((TypeDeclarationSyntax)syntax.Members[0], "Type.", source);
            var structure = (StructDeclarationSyntax)syntax.Members[1];
            AssertDocumentation(structure, "Struct.", source);
            AssertDocumentation(structure.Fields[0], "Field.", source);
            var enumeration = (EnumDeclarationSyntax)syntax.Members[2];
            AssertDocumentation(enumeration, "Enum.", source);
            AssertDocumentation(enumeration.Variants[0], "Variant.", source);
            AssertDocumentation(enumeration.Variants[0].NamedPayloadFields[0],
                "Payload field.", source);
            AssertDocumentation((FunctionDeclarationSyntax)syntax.Members[3],
                "Function.", source);
            AssertDocumentation((FunctionDeclarationSyntax)syntax.Members[4],
                "Extern function.", source);
            var implementation = (ImplDeclarationSyntax)syntax.Members[5];
            Assert.That(implementation, Is.Not.InstanceOf<IDocumentableSyntax>());
            AssertDocumentation(implementation.Methods[0], "Receiver method.", source);
            AssertDocumentation(implementation.Methods[1], "Associated function.", source);
            AssertDocumentation((ConstDeclarationSyntax)syntax.Members[6], "Constant.", source);
            var state = (StateBlockDeclarationSyntax)syntax.Members[7];
            AssertDocumentation(state.Members[0], "Private state.", source);
            AssertDocumentation(state.Members[1], "Public state.", source);
            AssertDocumentation(state.Members[2], "Sync state.", source);
            var behavior = (BehaviorDeclarationSyntax)syntax.Members[8];
            AssertDocumentation((EventDeclarationSyntax)behavior.Members[0], "Event.", source);
            AssertDocumentation((ReceiveDeclarationSyntax)behavior.Members[1], "Receive.", source);
        }

        [TestCase("/// EOF")]
        [TestCase("/// Blank.\n\nfn foo() {}")]
        [TestCase("/// Line.\n// ordinary\nfn foo() {}")]
        [TestCase("/// Block.\n/* ordinary */\nfn foo() {}")]
        [TestCase("/// Use.\nuse foo;")]
        [TestCase("/// Mod.\nmod foo;")]
        [TestCase("/// Impl.\nimpl f32 {}")]
        [TestCase("fn foo() {\n    /// Local.\n    let value = 1;\n}")]
        [TestCase("fn foo() {\n    /// Statement.\n    1;\n}")]
        public void Parser_ReportsOrphanDocumentationWithoutUnexpectedTokenDiagnostics(
            string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(HasCode(parser.Diagnostics.Diagnostics, "SBK1051"), Is.True);
            Assert.That(HasCode(parser.Diagnostics.Diagnostics, "SBK1002"), Is.False,
                Format(parser.Diagnostics.Diagnostics));
        }

        [Test]
        public void Parser_ReportsEarlierSeparatedBlockAndAttachesLaterBlock()
        {
            const string source = "/// Orphan.\n\n/// Attached.\nfn foo() {}";
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(CountCode(parser.Diagnostics.Diagnostics, "SBK1051"), Is.EqualTo(1));
            AssertDocumentation((FunctionDeclarationSyntax)syntax.Members[0],
                "Attached.", source);
        }

        [Test]
        public void Parser_PreservesDocumentationForPrivateAndPublicReceivers()
        {
            const string source = @"behavior {
    /// Private endpoint.
    receive private_ping {}
    /// Public endpoint.
    pub receive public_ping {}
}";
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var behavior = (BehaviorDeclarationSyntax)syntax.Members[0];
            AssertDocumentation((ReceiveDeclarationSyntax)behavior.Members[0],
                "Private endpoint.", source);
            AssertDocumentation((ReceiveDeclarationSyntax)behavior.Members[1],
                "Public endpoint.", source);
        }

        [Test]
        public void Parser_PreservesLangPrefixAssociationForDocumentableDeclarations()
        {
            const string source = @"/// Foo.
lang ""maybe""
struct Foo {}";
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            AssertDocumentation((StructDeclarationSyntax)syntax.Members[0], "Foo.", source);
        }

        [Test]
        public void Parser_ReportsLangPrefixedImplDocumentationAsOrphan()
        {
            var parser = new SobakasuParser(SourceText.From(@"/// Foo.
lang ""maybe""
impl Foo {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(HasCode(parser.Diagnostics.Diagnostics, "SBK1051"), Is.True);
            Assert.That(syntax.Members[0], Is.Not.InstanceOf<IDocumentableSyntax>());
        }

        [Test]
        public void Syntax_ModelLimitsDocumentabilityToDeclarations()
        {
            Assert.That(typeof(IDocumentableSyntax).IsAssignableFrom(typeof(ImplDeclarationSyntax)), Is.False);
            Assert.That(typeof(IDocumentableSyntax).IsAssignableFrom(typeof(UseDirectiveSyntax)), Is.False);
            Assert.That(typeof(IDocumentableSyntax).IsAssignableFrom(typeof(ModDeclarationSyntax)), Is.False);
            Assert.That(typeof(IDocumentableSyntax).IsAssignableFrom(typeof(StatementSyntax)), Is.False);
            Assert.That(typeof(IDocumentableSyntax).IsAssignableFrom(typeof(ExpressionSyntax)), Is.False);
            Assert.That(typeof(SyntaxNode).GetProperty("Documentation"), Is.Null);
        }

        private static void AssertDocumentation(
            IDocumentableSyntax declaration,
            string markdown,
            string source)
        {
            Assert.That(declaration.Documentation, Is.Not.Null);
            Assert.That(declaration.Documentation.Markdown, Is.EqualTo(markdown));
            var expectedStart = source.IndexOf("/// " + markdown,
                System.StringComparison.Ordinal);
            Assert.That(declaration.Documentation.Span.Start, Is.EqualTo(expectedStart));
            Assert.That(declaration.Documentation.Span.Length,
                Is.EqualTo(("/// " + markdown).Length));
        }

        private static bool HasCode(IReadOnlyList<Diagnostic> diagnostics, string code)
        {
            return CountCode(diagnostics, code) > 0;
        }

        private static int CountCode(IReadOnlyList<Diagnostic> diagnostics, string code)
        {
            var count = 0;
            foreach (var diagnostic in diagnostics)
                if (diagnostic.Code == code)
                    count++;
            return count;
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
