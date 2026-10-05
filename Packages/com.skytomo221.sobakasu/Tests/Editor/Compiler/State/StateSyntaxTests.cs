using System;
using System.Collections.Generic;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

using static Skytomo221.Sobakasu.Tests.Editor.StateCompilerTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class StateSyntaxTests
    {

        [Test]
        public void Lexer_RecognizesStateKeywordsAndKeepsModesContextual()
        {
            var tokens = LexAll("pub sync(none) state linear = smooth;");

            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.PubKeyword));
            Assert.That(tokens[1].Kind, Is.EqualTo(SyntaxKind.SyncKeyword));
            Assert.That(tokens[3].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[3].Text, Is.EqualTo("none"));
            Assert.That(tokens[5].Kind, Is.EqualTo(SyntaxKind.StateKeyword));
            Assert.That(tokens[6].Kind, Is.EqualTo(SyntaxKind.Identifier));
            Assert.That(tokens[8].Kind, Is.EqualTo(SyntaxKind.Identifier));
        }

        [Test]
        public void Parser_ParsesPublicSynchronizedStateAndFollowingEvent()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"state { pub sync(linear) value: f32 = field; }
behavior { on interact(state) { state.value = 1.0; } }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members.Count, Is.EqualTo(2));
            var state = ((StateBlockDeclarationSyntax)syntax.Members[0]).Members[0];
            Assert.That(state, Is.Not.Null);
            Assert.That(state.PubKeyword, Is.Not.Null);
            Assert.That(state.StateKeyword, Is.Null);
            Assert.That(state.MutKeyword, Is.Null);
            Assert.That(state.Identifier.Text, Is.EqualTo("value"));
            Assert.That(state.SynchronizationModifier.Mode,
                Is.EqualTo(SynchronizationModeSyntaxKind.Linear));
            Assert.That(state.FieldKeyword, Is.Not.Null);
            Assert.That(state.Initializer, Is.Null);
            Assert.That(((BehaviorDeclarationSyntax)syntax.Members[1]).Members[0], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_ParsesPrivateAndPublicConstants()
        {
            var parser = new SobakasuParser(SourceText.From(
                "const X = 1; pub const Y: i32 = X + 1;"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(2));
            var privateConstant = syntax.Members[0] as ConstDeclarationSyntax;
            var publicConstant = syntax.Members[1] as ConstDeclarationSyntax;
            Assert.That(privateConstant, Is.Not.Null);
            Assert.That(privateConstant.PubKeyword, Is.Null);
            Assert.That(publicConstant, Is.Not.Null);
            Assert.That(publicConstant.PubKeyword, Is.Not.Null);
            Assert.That(publicConstant.TypeClause, Is.Not.Null);
        }

        [TestCase("state { sync pub value: i32 = 0; }", "SBK1012")]
        [TestCase("state { pub pub value: i32 = 0; }", "SBK1013")]
        [TestCase("state { sync() value = 0; }", "SBK1011")]
        [TestCase("state { sync(unknown) value = 0; }", "SBK1010")]
        [TestCase("state { sync(linear, smooth) value = 0; }", "SBK1011")]
        [TestCase("state { sync(linear smooth) value = 0; }", "SBK1011")]
        [TestCase("behavior { on interact() { pub let value = 0; } }", "SBK1014")]
        [TestCase("behavior { on interact() { sync let mut value = 0; } }", "SBK1015")]
        [TestCase("pub sync(linear) fn value() {}", "SBK1016")]
        [TestCase("state { value: i32; }", "SBK1017")]
        [TestCase("let value = 0;", "SBK1033")]
        [TestCase("let mut value = 0;", "SBK1033")]
        [TestCase("pub let value = 0;", "SBK1033")]
        [TestCase("sync let mut value = 0;", "SBK1033")]
        [TestCase("state { mut value = 0; }", "SBK1034")]
        [TestCase("sync const VALUE = 0;", "SBK1035")]
        [TestCase("behavior { on interact { const VALUE = 0; } }", "SBK1036")]
        [TestCase("behavior { on interact { state value = 0; } }", "SBK1036")]
        [TestCase("const VALUE;", "SBK1037")]
        public void Parser_ReportsStateSyntaxDiagnostics(string source, string code)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, code), Is.True,
                Format(parser.Diagnostics.Diagnostics));
        }

        [TestCase("state { pub value: i32 = 1; }")]
        [TestCase("state { pub sync value: i32 = 1; }")]
        [TestCase("state { pub sync(linear) value: f32 = 1.0; }")]
        [TestCase("state { pub value: i32 = field; }")]
        public void Parser_AllowsPublicStateInitializers(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));
        }

        [TestCase("state { sync value = 0; }", "None")]
        [TestCase("state { sync(none) value = 0; }", "None")]
        [TestCase("state { sync(linear) value: f32 = 0.0; }", "Linear")]
        [TestCase("state { sync(smooth) value: f32 = 0.0; }", "Smooth")]
        public void Parser_ParsesAllSynchronizationForms(
            string source,
            string expectedMode)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var state = ((StateBlockDeclarationSyntax)syntax.Members[0]).Members[0];
            Assert.That(state, Is.Not.Null);
            Assert.That(state.SynchronizationModifier.Mode.ToString(), Is.EqualTo(expectedMode));
        }

        [TestCase("state { pub value: i32 = field; }")]
        [TestCase("state { pub sync value: i32 = field; }")]
        [TestCase("state { pub sync(linear) value: f32 = field; }")]
        [TestCase("state { private_value = 1; }")]
        [TestCase("state { sync synchronized_private = 1; }")]
        public void Parser_ParsesRequiredStateForms(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(1));
            Assert.That(syntax.Members[0], Is.TypeOf<StateBlockDeclarationSyntax>());
        }

        [Test]
        public void Parser_RecoversFromMalformedStateBeforeFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"state { sync(unknown) value = 0; }
fn read() -> i32 { return 1; }
behavior { on interact() { extern UnityEngine.Debug.Log(read()); } }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1010"), Is.True);
            Assert.That(syntax.Members.Count, Is.EqualTo(3));
            Assert.That(syntax.Members[1], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members[2], Is.TypeOf<BehaviorDeclarationSyntax>());
        }

        [Test]
        public void Parser_ConsumesPublicInitializerAndPreservesFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"state { pub value: i32 = unknown_function(); }
fn read() -> i32 { return 1; }
behavior { on interact() { extern UnityEngine.Debug.Log(read()); } }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(3));
            Assert.That(((StateBlockDeclarationSyntax)syntax.Members[0]).Members[0].Initializer, Is.Not.Null);
            Assert.That(syntax.Members[1], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members[2], Is.TypeOf<BehaviorDeclarationSyntax>());
        }
    }
}
