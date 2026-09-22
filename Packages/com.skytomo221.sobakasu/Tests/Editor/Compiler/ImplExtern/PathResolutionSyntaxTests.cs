using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class PathResolutionSyntaxTests
    {
        [Test]
        public void Lexer_DistinguishesColonFromDoubleColon()
        {
            var lexer = new SobakasuLexer(SourceText.From("x: i32; Foo::new()"));
            Assert.That(lexer.Lex().Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(lexer.Lex().Kind, Is.EqualTo(SyntaxKind.Colon));
            Assert.That(lexer.Lex().Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(lexer.Lex().Kind, Is.EqualTo(SyntaxKind.Semicolon));
            Assert.That(lexer.Lex().Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(lexer.Lex().Kind, Is.EqualTo(SyntaxKind.DoubleColonToken));
        }

        [Test]
        public void Parser_SeparatesPathsValueMembersAndReceivers()
        {
            var parser = new SobakasuParser(SourceText.From(@"
use core::string;
impl Foo {
  fn create(value: i32) -> Foo { Foo::new(value) }
  fn update(self, value: i32) { self.value = value; }
}
on start { foo.update(); Result::Ok(value); }
"));
            var unit = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.False);
            var impl = (ImplDeclarationSyntax)unit.Members[1];
            Assert.That(impl.Methods[0].Parameters, Has.Count.EqualTo(1));
            Assert.That(impl.Methods[1].Parameters[0], Is.TypeOf<SelfParameterSyntax>());

            var callStatement = (ExpressionStatementSyntax)((EventDeclarationSyntax)
                unit.Members[2]).Body.Statements[0];
            var call = (CallExpressionSyntax)callStatement.Expression;
            Assert.That(call.Target, Is.TypeOf<MemberAccessExpressionSyntax>());
            var enumStatement = (ExpressionStatementSyntax)((EventDeclarationSyntax)
                unit.Members[2]).Body.Statements[1];
            Assert.That(((CallExpressionSyntax)enumStatement.Expression).Target,
                Is.TypeOf<PathExpressionSyntax>());
        }

        [Test]
        public void Parser_SeparatesExternalIdentitiesFromSobakasuPaths()
        {
            var parser = new SobakasuParser(SourceText.From(@"
type Foo = extern External.Namespace.Foo;
enum Bar = extern External.Namespace.Bar {
  Value = extern Value,
}
impl Foo = extern External.Namespace.Foo {
  fn create() = extern External.Namespace.Foo.Create()
  fn update(self) = extern self.Update()
}
"));
            var unit = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.False);
            var type = (TypeDeclarationSyntax)unit.Members[0];
            Assert.That(type.ExternalTypeName.GetText(),
                Is.EqualTo("External.Namespace.Foo"));
            Assert.That(type.ExternalTypeName.SeparatorTokens,
                Has.All.Property("Kind").EqualTo(SyntaxKind.Dot));

            var impl = (ImplDeclarationSyntax)unit.Members[2];
            var create = (CallExpressionSyntax)impl.Methods[0]
                .ExternalBinding.ExternExpression.Expression;
            Assert.That(create.Target, Is.TypeOf<MemberAccessExpressionSyntax>());
            var update = (CallExpressionSyntax)impl.Methods[1]
                .ExternalBinding.ExternExpression.Expression;
            Assert.That(update.Target, Is.TypeOf<MemberAccessExpressionSyntax>());
        }

        [Test]
        public void Parser_RejectsPathSeparatorsInExternalTypeIdentities()
        {
            var parser = new SobakasuParser(SourceText.From(
                "type Foo = extern External::Namespace::Foo;"));

            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
        }

        [Test]
        public void Binder_RejectsDottedAssociatedFunctionCalls()
        {
            var result = SobakasuTestCompiler.CompileWithoutStandardLibrary(@"
impl i32 {
  fn create(value: i32) -> i32 { value }
}
on interact { i32.create(1); }");

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorText, Does.Contain("SBK3068"));
        }

        [TestCase("impl Foo { fn invalid(value: i32, self) {} }")]
        [TestCase("impl Foo { fn invalid(self: Foo) {} }")]
        [TestCase("impl Foo { static fn invalid() {} }")]
        public void Parser_RejectsRemovedOrInvalidReceiverForms(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.HasErrors, Is.True);
        }
    }
}
