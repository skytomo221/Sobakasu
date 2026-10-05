using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class KeywordVocabularyTests
    {
        [Test]
        public void Lexer_RecognizesAdr0055Keywords()
        {
  var tokens = LexAll("function implementation mutable public module language item");
  Assert.That(tokens.Select(token => token.Kind), Is.EqualTo(new[]
  {
      SyntaxKind.FunctionKeyword,
      SyntaxKind.ImplementationKeyword,
      SyntaxKind.MutableKeyword,
      SyntaxKind.PublicKeyword,
      SyntaxKind.ModuleKeyword,
      SyntaxKind.LanguageKeyword,
      SyntaxKind.ItemKeyword,
  }));
        }

        [Test]
        public void Lexer_TreatsRemovedSpellingsAsIdentifiers()
        {
  var tokens = LexAll("fn impl mut pub mod lang");
  Assert.That(tokens, Has.Count.EqualTo(6));
  Assert.That(tokens.All(token => token.Kind == SyntaxKind.Identifier), Is.True);
        }

        [Test]
        public void Parser_ParsesLanguageItemAsTwoKeywords()
        {
  var parser = new SobakasuParser(SourceText.From(
      "language item \"maybe\"\npublic enum Maybe<T> { Nothing, Just(T), }"));
  var unit = parser.ParseCompilationUnit();

  Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);
  var declaration = (EnumDeclarationSyntax)unit.Members.Single();
  Assert.That(declaration.LanguageItem.LanguageKeyword.Kind, Is.EqualTo(SyntaxKind.LanguageKeyword));
  Assert.That(declaration.LanguageItem.ItemKeyword.Kind, Is.EqualTo(SyntaxKind.ItemKeyword));
  Assert.That(declaration.LanguageItem.Item.Value, Is.EqualTo("maybe"));
        }

        [TestCase("language")]
        [TestCase("language \"maybe\"")]
        [TestCase("language item")]
        public void Parser_DiagnosesIncompleteLanguageItemPrefix(string source)
        {
  var parser = new SobakasuParser(SourceText.From(source));
  parser.ParseCompilationUnit();
  Assert.That(parser.Diagnostics.HasErrors, Is.True);
        }

        [Test]
        public void Binder_AllowsRemovedSpellingsAsLocalIdentifiers()
        {
  var source = @"function test() {
  let fn = 1;
  let impl = 2;
  let mut = 3;
  let pub = 4;
  let mod = 5;
  let lang = 6;
}";
  var diagnostics = SobakasuCompiler.ValidateDeclarations(source, SobakasuTestEnvironment.Default);
  Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void Binder_PreservesMutableAndImmutableRules()
        {
  var mutableDiagnostics = SobakasuCompiler.ValidateDeclarations(
      "function test() { let mutable value = 0; value = 1; }",
      SobakasuTestEnvironment.Default);
  Assert.That(mutableDiagnostics, Is.Empty);

  var immutableDiagnostics = SobakasuCompiler.ValidateDeclarations(
      "function test() { let value = 0; value = 1; }",
      SobakasuTestEnvironment.Default);
  Assert.That(immutableDiagnostics, Is.Not.Empty);
        }

        private static List<SyntaxToken> LexAll(string source)
        {
  var lexer = new SobakasuLexer(SourceText.From(source));
  var result = new List<SyntaxToken>();
  while (true)
  {
      var token = lexer.Lex();
      if (token.Kind == SyntaxKind.EndOfFile)
          break;
      result.Add(token);
  }
  Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty);
  return result;
        }
    }
}
