using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Desugar;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Optimizer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;
using UnityEditor;
using UnityEngine;

using static Skytomo221.Sobakasu.Tests.Editor.ImplExternTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class ImplExternSyntaxTests
    {
        private readonly List<string> _cleanupAssetPaths = new();

        [TearDown]
        public void TearDown()
        {
            if (_cleanupAssetPaths.Count == 0)
            {
                return;
            }

            _cleanupAssetPaths.Sort((left, right) => right.Length.CompareTo(left.Length));
            foreach (var assetPath in _cleanupAssetPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null ||
                    AssetDatabase.IsValidFolder(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }

            _cleanupAssetPaths.Clear();
            AssetDatabase.Refresh();
        }

        [Test]
        public void Lexer_RecognizesImplExternSelfStaticAndOperatorNameTokens()
        {
            var tokens = LexAll("impl extern self Self static @+ @- @! @~");

            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.ImplKeyword));
            Assert.That(tokens[1].Kind, Is.EqualTo(SyntaxKind.ExternKeyword));
            Assert.That(tokens[2].Kind, Is.EqualTo(SyntaxKind.SelfKeyword));
            Assert.That(tokens[3].Kind, Is.EqualTo(SyntaxKind.SelfTypeKeyword));
            Assert.That(tokens[4].Kind, Is.EqualTo(SyntaxKind.StaticKeyword));
            Assert.That(tokens[5].Kind, Is.EqualTo(SyntaxKind.AtToken));
            Assert.That(tokens[6].Kind, Is.EqualTo(SyntaxKind.PlusToken));
            Assert.That(tokens[7].Kind, Is.EqualTo(SyntaxKind.AtToken));
            Assert.That(tokens[8].Kind, Is.EqualTo(SyntaxKind.MinusToken));
            Assert.That(tokens[9].Kind, Is.EqualTo(SyntaxKind.AtToken));
            Assert.That(tokens[10].Kind, Is.EqualTo(SyntaxKind.BangToken));
            Assert.That(tokens[11].Kind, Is.EqualTo(SyntaxKind.AtToken));
            Assert.That(tokens[12].Kind, Is.EqualTo(SyntaxKind.TildeToken));
        }

        [Test]
        public void Parser_ParsesExternalAndAdditionalImplMethods()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"pub impl GameObject = extern UnityEngine.GameObject {
  pub fn set_active(active: bool) { extern self.SetActive(active); }
  pub fn active? -> bool { extern self.activeSelf }
  pub static fn find(name: string) -> Self { extern UnityEngine.GameObject.Find(name) }
}
impl GameObject {
  pub fn @- -> Self { extern -self }
  pub fn +(rhs: Self) -> Self { extern self + rhs }
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(2));

            var external = syntax.Members[0] as ImplDeclarationSyntax;
            Assert.That(external, Is.Not.Null);
            Assert.That(external.PubKeyword, Is.Not.Null);
            Assert.That(external.IsExternalBinding, Is.True);
            Assert.That(external.TargetType.GetText(), Is.EqualTo("GameObject"));
            Assert.That(external.ExternalTypeName.GetText(),
                Is.EqualTo("UnityEngine.GameObject"));
            Assert.That(external.Methods, Has.Count.EqualTo(3));
            Assert.That(external.Methods[1].Name, Is.EqualTo("active?"));
            Assert.That(external.Methods[1].OpenParenToken, Is.Null);
            Assert.That(external.Methods[2].StaticKeyword, Is.Not.Null);

            var additional = syntax.Members[1] as ImplDeclarationSyntax;
            Assert.That(additional, Is.Not.Null);
            Assert.That(additional.IsExternalBinding, Is.False);
            Assert.That(additional.Methods[0].Name, Is.EqualTo("@-"));
            Assert.That(additional.Methods[0].Parameters, Is.Empty);
            Assert.That(additional.Methods[1].Name, Is.EqualTo("+"));
            Assert.That(additional.Methods[1].Parameters, Has.Count.EqualTo(1));
        }

        [TestCase("extern UnityEngine.Debug.Log(\"hello\");")]
        [TestCase("extern target.SetActive(true);")]
        [TestCase("extern target.activeSelf;")]
        [TestCase("extern target.name = \"name\";")]
        [TestCase("extern new UnityEngine.Vector3(1.0f32, 2.0f32, 3.0f32);")]
        [TestCase("extern -value;")]
        [TestCase("extern value + value;")]
        [TestCase("let next = (extern target.layer) + 1;")]
        public void Parser_ParsesSupportedExternExpressionShapes(string statement)
        {
            var parser = new SobakasuParser(SourceText.From(
                $"on interact {{ {statement} }}"));
            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
        }

        [TestCase("pub fn %(rhs: Self) -> Self = extern self % rhs")]
        [TestCase("pub fn >(rhs: Self) -> bool = extern self > rhs")]
        public void Parser_KeepsDeclarativeComparisonSeparateFromFollowingMethod(string followingMethod)
        {
            var parser = new SobakasuParser(SourceText.From($@"
impl i32 {{
  pub fn <(rhs: Self) -> bool = extern self < rhs
  {followingMethod}
}}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var declaration = (ImplDeclarationSyntax)syntax.Members.Single();
            Assert.That(declaration.Methods, Has.Count.EqualTo(2));
            Assert.That(declaration.Methods[0].ExternalBinding.ExternExpression.Expression,
                Is.TypeOf<BinaryExpressionSyntax>());
        }

        [Test]
        public void Parser_RecoversAfterInvalidAtOperatorName()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"impl i32 {
  fn @invalid -> i32 { 0 }
  fn valid -> i32 { 1 }
}
on interact {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members[^1], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_RecoversAfterInvalidExternExpression()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"on interact { extern ; }
on update {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members, Has.Count.EqualTo(2));
            Assert.That(syntax.Members[1], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_ParsesGenericFunctionAndCallableApplications()
        {
            var parser = new SobakasuParser(SourceText.From(@"
fn foo<T, U>() -> T = extern Test.Api.Foo<T, U>();
on start {
  foo<i32, string>();
  receiver.foo<string>();
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = (FunctionDeclarationSyntax)syntax.Members[0];
            Assert.That(function.GenericParameters.Parameters.Select(token => token.Text),
                Is.EqualTo(new[] { "T", "U" }));
            var firstCall = (ExpressionStatementSyntax)((EventDeclarationSyntax)
                syntax.Members[1]).Body.Statements[0];
            var call = (CallExpressionSyntax)firstCall.Expression;
            Assert.That(call.Target, Is.TypeOf<GenericTypeExpressionSyntax>());
            Assert.That(((GenericTypeExpressionSyntax)call.Target)
                .TypeArgumentList.Arguments, Has.Count.EqualTo(2));
        }

        [TestCase("pub fn foo = extern Foo.Bar()", false, false)]
        [TestCase("pub fn foo -> SomeType = extern Foo.Bar()", true, false)]
        [TestCase("pub fn foo(value: i32) = extern Foo.Bar(value)", false, false)]
        [TestCase("pub fn foo(value: i32) -> SomeType = extern Foo.Bar(value)", true, false)]
        [TestCase("pub fn foo(value: string) = maybe extern Foo.Find(value)", false, true)]
        public void Parser_ParsesDeclarativeExternBindings(
            string source,
            bool hasReturnType,
            bool isMaybe)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = syntax.Members[0] as FunctionDeclarationSyntax;
            Assert.That(function, Is.Not.Null);
            Assert.That(function.Body, Is.Null);
            Assert.That(function.ExternalBinding, Is.Not.Null);
            Assert.That(function.ExternalBinding.IsMaybe, Is.EqualTo(isMaybe));
            Assert.That(function.ReturnTypeAnnotation != null, Is.EqualTo(hasReturnType));
        }

        [Test]
        public void Parser_RejectsGeneralExpressionBodiedFunctionAndRecovers()
        {
            var parser = new SobakasuParser(SourceText.From(
                "pub fn bad = 123 pub fn good { }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1038"), Is.True,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(2));
            Assert.That(((FunctionDeclarationSyntax)syntax.Members[1]).Name,
                Is.EqualTo("good"));
        }

        [Test]
        public void Parser_ParsesMaybeOutForMethodAndConstructorAbiSignatures()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"fn single() -> Maybe<Test.Owner>
  = extern Test.Api.TryGet(maybe out Test.Owner owner)
fn pair() -> (bool, Maybe<Test.Owner>)
  = extern Test.Api.TryGet(maybe out Test.Owner owner)
pub impl Foo = extern Test.Foo {
  pub static fn create() -> (Self, Maybe<Test.Owner>)
    = extern new Self(maybe out Test.Owner owner)
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var single = (FunctionDeclarationSyntax)syntax.Members[0];
            Assert.That(single.ExternalBinding.AbiSignature.Parameters[0].IsMaybe,
                Is.True);
            Assert.That(
                single.ExternalBinding.AbiSignature.Parameters[0].Modifier.Kind,
                Is.EqualTo(SyntaxKind.OutKeyword));
            var pair = (FunctionDeclarationSyntax)syntax.Members[1];
            Assert.That(pair.ReturnTypeAnnotation.Type.GetText(),
                Is.EqualTo("(bool, Maybe<Test.Owner>)"));

            var impl = (ImplDeclarationSyntax)syntax.Members[2];
            var constructor = impl.Methods[0].ExternalBinding.AbiSignature;
            Assert.That(constructor.IsConstructor, Is.True);
            Assert.That(constructor.ConstructorType.GetText(), Is.EqualTo("Self"));
            Assert.That(constructor.Parameters[0].IsMaybe, Is.True);
        }

        [TestCase("maybe ref Test.Owner owner")]
        [TestCase("maybe Test.Owner owner")]
        public void Parser_RejectsMaybeOnNonOutAbiParameters(string parameter)
        {
            var parser = new SobakasuParser(SourceText.From(
                $"fn invalid() = extern Test.Api.TryGet({parameter})"));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1039"),
                Is.True, Format(parser.Diagnostics.Diagnostics));
        }

        [Test]
        public void Parser_DoesNotIntroduceMaybeOutForOrdinaryFunctionParameters()
        {
            var parser = new SobakasuParser(SourceText.From(
                "fn invalid(maybe out Test.Owner owner) {}"));
            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
        }
    }
}
