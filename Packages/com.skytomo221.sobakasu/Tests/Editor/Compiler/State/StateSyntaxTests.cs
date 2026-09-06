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
using UnityEditor;
using UnityEngine;

using static Skytomo221.Sobakasu.Tests.Editor.StateTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class StateSyntaxTests
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
                @"pub sync(linear) state value: f32;
on interact() { value = 1.0; }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members.Count, Is.EqualTo(2));
            var state = syntax.Members[0] as StateDeclarationSyntax;
            Assert.That(state, Is.Not.Null);
            Assert.That(state.PubKeyword, Is.Not.Null);
            Assert.That(state.StateKeyword.Kind, Is.EqualTo(SyntaxKind.StateKeyword));
            Assert.That(state.MutKeyword, Is.Null);
            Assert.That(state.Identifier.Text, Is.EqualTo("value"));
            Assert.That(state.SynchronizationModifier.Mode,
                Is.EqualTo(SynchronizationModeSyntaxKind.Linear));
            Assert.That(state.EqualsToken, Is.Null);
            Assert.That(state.Initializer, Is.Null);
            Assert.That(syntax.Members[1], Is.TypeOf<EventDeclarationSyntax>());
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

        [TestCase("sync pub state value: i32;", "SBK1012")]
        [TestCase("pub pub state value: i32;", "SBK1013")]
        [TestCase("sync() state value = 0;", "SBK1011")]
        [TestCase("sync(unknown) state value = 0;", "SBK1010")]
        [TestCase("sync(linear, smooth) state value = 0;", "SBK1011")]
        [TestCase("sync(linear smooth) state value = 0;", "SBK1011")]
        [TestCase("on interact() { pub let value = 0; }", "SBK1014")]
        [TestCase("on interact() { sync let mut value = 0; }", "SBK1015")]
        [TestCase("pub sync(linear) fn value() {}", "SBK1016")]
        [TestCase("state value;", "SBK1017")]
        [TestCase("let value = 0;", "SBK1033")]
        [TestCase("let mut value = 0;", "SBK1033")]
        [TestCase("pub let value = 0;", "SBK1033")]
        [TestCase("sync let mut value = 0;", "SBK1033")]
        [TestCase("state mut value = 0;", "SBK1034")]
        [TestCase("sync const VALUE = 0;", "SBK1035")]
        [TestCase("on interact { const VALUE = 0; }", "SBK1036")]
        [TestCase("on interact { state value = 0; }", "SBK1036")]
        [TestCase("const VALUE;", "SBK1037")]
        public void Parser_ReportsStateSyntaxDiagnostics(string source, string code)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, code), Is.True,
                Format(parser.Diagnostics.Diagnostics));
        }

        [TestCase("pub state value = 1;", "SBK1040")]
        [TestCase("pub state value: i32 = 1;", "SBK1040")]
        [TestCase("pub sync state value: i32 = 1;", "SBK1040")]
        [TestCase("pub sync(linear) state value: f32 = 1.0;", "SBK1040")]
        [TestCase("pub state value;", "SBK1041")]
        public void Parser_ReportsPublicStateOwnershipDiagnostics(string source, string code)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, code), Is.True,
                Format(parser.Diagnostics.Diagnostics));
        }

        [TestCase("sync state value = 0;", "None")]
        [TestCase("sync(none) state value = 0;", "None")]
        [TestCase("sync(linear) state value: f32 = 0.0;", "Linear")]
        [TestCase("sync(smooth) state value: f32 = 0.0;", "Smooth")]
        public void Parser_ParsesAllSynchronizationForms(
            string source,
            string expectedMode)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var state = syntax.Members[0] as StateDeclarationSyntax;
            Assert.That(state, Is.Not.Null);
            Assert.That(state.SynchronizationModifier.Mode.ToString(), Is.EqualTo(expectedMode));
        }

        [TestCase("pub state value: i32;")]
        [TestCase("pub sync state value: i32;")]
        [TestCase("pub sync(linear) state value: f32;")]
        [TestCase("state private_value = 1;")]
        [TestCase("sync state synchronized_private = 1;")]
        public void Parser_ParsesRequiredStateForms(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members, Has.Count.EqualTo(1));
            Assert.That(syntax.Members[0], Is.TypeOf<StateDeclarationSyntax>());
        }

        [Test]
        public void Parser_RecoversFromMalformedStateBeforeFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"sync(unknown) state value = 0;
fn read() -> i32 { return value; }
on interact() { extern UnityEngine.Debug.Log(read()); }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1010"), Is.True);
            Assert.That(syntax.Members.Count, Is.EqualTo(3));
            Assert.That(syntax.Members[1], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members[2], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_ConsumesForbiddenPublicInitializerAndPreservesFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"pub state value: i32 = unknown_function();
fn read() -> i32 { return value; }
on interact() { extern UnityEngine.Debug.Log(read()); }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Has.Count.EqualTo(1),
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(ContainsCode(parser.Diagnostics.Diagnostics, "SBK1040"), Is.True);
            Assert.That(syntax.Members, Has.Count.EqualTo(3));
            Assert.That(((StateDeclarationSyntax)syntax.Members[0]).Initializer, Is.Not.Null);
            Assert.That(syntax.Members[1], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members[2], Is.TypeOf<EventDeclarationSyntax>());
        }
    }
}
