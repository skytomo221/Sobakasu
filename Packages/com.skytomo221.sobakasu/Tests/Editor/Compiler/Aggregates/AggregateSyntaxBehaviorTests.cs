using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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

using static Skytomo221.Sobakasu.Tests.Editor.AggregateTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class AggregateSyntaxBehaviorTests : AggregateTestFixture
    {


        [Test]
        public void Lexer_RecognizesStructAndEnumKeywords()
        {
            var tokens = LexAll("struct Point {} enum State {}");

            Assert.That(tokens[0].Kind, Is.EqualTo(SyntaxKind.StructKeyword));
            Assert.That(tokens[4].Kind, Is.EqualTo(SyntaxKind.EnumKeyword));
        }

        [Test]
        public void Parser_ParsesGenericDeclarationsExplicitTypesAndNestedGreaterTokens()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"struct Pair<T, U> { first: T, second: U, }
enum Option<T> { None, Some(T), }
impl<T> Option<T> {}
on start {
  let explicit: Pair<i32, string> = Pair<i32, string> { first: 1, second: ""x"", };
  let nested: Option<Option<i32>> = Option::Some(Option::Some(1));
  let shifted = 8 >> 1;
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var pair = syntax.Members[0] as StructDeclarationSyntax;
            var option = syntax.Members[1] as EnumDeclarationSyntax;
            var impl = syntax.Members[2] as ImplDeclarationSyntax;
            Assert.That(pair.GenericParameters.Parameters.Count, Is.EqualTo(2));
            Assert.That(option.GenericParameters.Parameters.Count, Is.EqualTo(1));
            Assert.That(impl.GenericParameters.Parameters.Count, Is.EqualTo(1));
            Assert.That(impl.TargetType.GetText(), Is.EqualTo("Option<T>"));
        }

        [Test]
        public void Parser_ParsesExternalStructAndEnumBindings()
        {
            var parser = new SobakasuParser(SourceText.From(@"
pub struct Vector = extern UnityEngine.Vector3 { x: f32 = extern x, }
pub enum Target = extern VRC.Udon.Common.Interfaces.NetworkEventTarget { All = extern All, }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty, Format(parser.Diagnostics.Diagnostics));
            var structure = (StructDeclarationSyntax)syntax.Members[0];
            var enumeration = (EnumDeclarationSyntax)syntax.Members[1];
            Assert.That(structure.IsExternalBinding, Is.True);
            Assert.That(structure.ExternalTypeName.GetText(), Is.EqualTo("UnityEngine.Vector3"));
            Assert.That(structure.Fields[0].Type.GetText(), Is.EqualTo("f32"));
            Assert.That(structure.Fields[0].ExternalMemberName.Text, Is.EqualTo("x"));
            Assert.That(enumeration.IsExternalBinding, Is.True);
            Assert.That(enumeration.Variants[0].ExternalMemberName.Text, Is.EqualTo("All"));
        }

        [Test]
        public void Parser_ParsesTupleTypesValuesAccessAndNestedBindingPatterns()
        {
            var accessTokens = LexAll("value.0.1");
            Assert.That(accessTokens.ConvertAll(token => token.Kind),
                Is.EqualTo(new[]
                {
                    SyntaxKind.Identifier,
                    SyntaxKind.Dot,
                    SyntaxKind.Int32Literal,
                    SyntaxKind.Dot,
                    SyntaxKind.Int32Literal,
                    SyntaxKind.EndOfFile
                }));

            var parser = new SobakasuParser(SourceText.From(
                @"fn value(input: (i32,)) -> ((i32,), string) {
  ((input.0,), ""value"")
}
fn unit() -> () { () }
on start {
  let ((number,), text) = value((42,));
  let grouped: i32 = (number);
  let _ = unit();
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = syntax.Members[0] as FunctionDeclarationSyntax;
            Assert.That(function.Parameters[0].Type.GetText(), Is.EqualTo("(i32,)"));
            Assert.That(function.ReturnTypeAnnotation.Type.GetText(),
                Is.EqualTo("((i32,), string)"));
            var start = syntax.Members[2] as EventDeclarationSyntax;
            var declaration = start.Body.Statements[0] as VariableDeclarationStatementSyntax;
            Assert.That(declaration.Pattern, Is.TypeOf<TupleBindingPatternSyntax>());
        }

        [Test]
        public void Parser_ParsesStructsMixedEnumsAndConstructionExpressions()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"struct Point { x: i32, y: i32, }
enum Event {
  None,
  Key(char),
  Pair(i32, string),
  Click { point: Point, button: i32, },
}
on start {
  let point = Point { y: 20, x: 10, };
  let none = Event::None;
  let pair = Event::Pair(1, ""two"");
  let click = Event::Click { button: 1, point: point, };
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members[0], Is.TypeOf<StructDeclarationSyntax>());
            var enumDeclaration = syntax.Members[1] as EnumDeclarationSyntax;
            Assert.That(enumDeclaration, Is.Not.Null);
            Assert.That(enumDeclaration.Variants.Count, Is.EqualTo(4));
            Assert.That(enumDeclaration.Variants[0].VariantKind,
                Is.EqualTo(EnumVariantSyntaxKind.Unit));
            Assert.That(enumDeclaration.Variants[1].TuplePayloadTypes.Count, Is.EqualTo(1));
            Assert.That(enumDeclaration.Variants[2].TuplePayloadTypes.Count, Is.EqualTo(2));
            Assert.That(enumDeclaration.Variants[3].VariantKind,
                Is.EqualTo(EnumVariantSyntaxKind.Struct));
            Assert.That(enumDeclaration.Variants[3].NamedPayloadFields.Count, Is.EqualTo(2));
            Assert.That(syntax.Members[2], Is.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Parser_RecoversFromMalformedAggregateBeforeFollowingMembers()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"struct Broken { value i32,
fn after() -> i32 { 1 }
enum AlsoBroken { Pair(i32, bool, }
on start {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members, Has.Some.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(syntax.Members, Has.Some.TypeOf<EventDeclarationSyntax>());
        }

        [Test]
        public void Lexer_RecognizesMatchFatArrowAndPreservesRelatedOperators()
        {
            var tokens = LexAll("match => = == > >= ->");

            Assert.That(tokens.ConvertAll(token => token.Kind), Is.EqualTo(new[]
            {
                SyntaxKind.MatchKeyword,
                SyntaxKind.FatArrowToken,
                SyntaxKind.EqualsToken,
                SyntaxKind.EqualsEqualsToken,
                SyntaxKind.GreaterToken,
                SyntaxKind.GreaterOrEqualsToken,
                SyntaxKind.ArrowToken,
                SyntaxKind.EndOfFile
            }));
        }

        [Test]
        public void Parser_ParsesMatchPatternsBlockArmsAndOptionalTrailingComma()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"enum Option { None, Some(i32), }
fn choose(value: Option) -> i32 {
  match value {
    Option::None => { 0 },
    Option::Some(value) => value
  }
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = (FunctionDeclarationSyntax)syntax.Members[1];
            var match = function.Body.TrailingExpression as MatchExpressionSyntax;
            Assert.That(match, Is.Not.Null);
            Assert.That(match.Expression, Is.TypeOf<NameExpressionSyntax>());
            Assert.That(match.Arms.Count, Is.EqualTo(2));
            Assert.That(match.Arms[0].Pattern, Is.TypeOf<EnumUnitVariantPatternSyntax>());
            Assert.That(match.Arms[0].Expression, Is.TypeOf<BlockExpressionSyntax>());
            Assert.That(match.Arms[0].CommaToken, Is.Not.Null);
            Assert.That(match.Arms[1].Pattern, Is.TypeOf<EnumTupleVariantPatternSyntax>());
            Assert.That(match.Arms[1].CommaToken, Is.Null);
        }

        [Test]
        public void Parser_DoesNotConsumeMatchScrutineeBraceAsAggregateInitializer()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"enum Choice { First, Second, }
fn choose(value: Choice) -> i32 {
  match value { Choice::First => 1, Choice::Second => 2, }
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = (FunctionDeclarationSyntax)syntax.Members[1];
            var match = (MatchExpressionSyntax)function.Body.TrailingExpression;
            Assert.That(match.Expression, Is.TypeOf<NameExpressionSyntax>());
            Assert.That(match.Arms.Count, Is.EqualTo(2));
        }

        [Test]
        public void Parser_RecoversFromMalformedMatchBeforeFollowingFunction()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"fn broken(value: i32) -> i32 {
  match value { 0 1, _ => 2, }
}
fn after() -> i32 { 3 }
on start {}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members.Count, Is.EqualTo(3));
            Assert.That(((FunctionDeclarationSyntax)syntax.Members[1]).Identifier.Text,
                Is.EqualTo("after"));
        }


    }
}
