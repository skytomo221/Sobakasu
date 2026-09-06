using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

using static Skytomo221.Sobakasu.Tests.Editor.ModuleTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class ModuleSyntaxTests
    {

        [Test]
        public void Parser_ParsesDottedSobakasuModulePathAndAlias()
        {
            var parser = new SobakasuParser(
                SourceText.From("use example.math.twice as double_value;"));
            var syntax = parser.ParseCompilationUnit();
            var use = syntax.Members[0] as UseDirectiveSyntax;

            Assert.That(use, Is.Not.Null);
            Assert.That(use.Path.GetText(), Is.EqualTo("example.math.twice"));
            Assert.That(use.Alias.Text, Is.EqualTo("double_value"));
            Assert.That(parser.Diagnostics.HasErrors, Is.False);
        }

        [Test]
        public void Parser_ParsesGroupedNestedSelfGlobAndLeafAliases()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"use foo.{A as X, self as f, bar.{B, C,}, *};
pub use foo.*;"));
            var syntax = parser.ParseCompilationUnit();

            var grouped = (UseDirectiveSyntax)syntax.Members[0];
            Assert.That(grouped.UseTree.Path.GetText(), Is.EqualTo("foo"));
            Assert.That(grouped.UseTree.Group.Items.Count, Is.EqualTo(4));
            Assert.That(grouped.UseTree.Group.Items[0].Alias.Text, Is.EqualTo("X"));
            Assert.That(grouped.UseTree.Group.Items[1].IsSelf, Is.True);
            Assert.That(grouped.UseTree.Group.Items[1].Alias.Text, Is.EqualTo("f"));
            Assert.That(grouped.UseTree.Group.Items[2].Group.Items.Count, Is.EqualTo(2));
            Assert.That(grouped.UseTree.Group.Items[3].IsGlob, Is.True);

            var publicGlob = (UseDirectiveSyntax)syntax.Members[1];
            Assert.That(publicGlob.IsReExport, Is.True);
            Assert.That(publicGlob.UseTree.IsGlob, Is.True);
            Assert.That(parser.Diagnostics.HasErrors, Is.False);
        }

        [TestCase("use foo.*;")]
        [TestCase("use foo.{*};")]
        [TestCase("use foo.{self,};")]
        [TestCase("use foo.{bar.*, Baz,};")]
        public void Parser_AcceptsGlobAndTrailingCommaForms(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.False);
        }

        [TestCase("use foo.{;")]
        [TestCase("use foo.{A,,B};")]
        [TestCase("use foo.{A B};")]
        [TestCase("use foo.{bar.{A, B};")]
        [TestCase("use foo.{A as};")]
        public void Parser_DiagnosesMalformedUseTreesAndRecovers(string source)
        {
            var parser = new SobakasuParser(SourceText.From(
                source + " pub fn after -> i32 { 1 }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.HasErrors, Is.True);
            Assert.That(syntax.Members.OfType<FunctionDeclarationSyntax>().Any(), Is.True);
        }

        [Test]
        public void Parser_RejectsDoubleColonModulePath()
        {
            var parser = new SobakasuParser(
                SourceText.From("use example::math::twice;"));
            parser.ParseCompilationUnit();

            Assert.That(ContainsCode(parser.Diagnostics, "SBK1024"), Is.True);
        }

        [Test]
        public void Parser_ParsesModPubModAndPubUse()
        {
            var parser = new SobakasuParser(SourceText.From(
                "mod private_child; pub mod public_child; pub use private_child.value;"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(syntax.Members[0], Is.TypeOf<ModDeclarationSyntax>());
            Assert.That(((ModDeclarationSyntax)syntax.Members[0]).IsPublic, Is.False);
            Assert.That(((ModDeclarationSyntax)syntax.Members[1]).IsPublic, Is.True);
            Assert.That(((UseDirectiveSyntax)syntax.Members[2]).IsReExport, Is.True);
            Assert.That(parser.Diagnostics.HasErrors, Is.False);
        }

        [Test]
        public void Parser_ReportsMalformedAndNestedModAndRecovers()
        {
            var malformed = new SobakasuParser(SourceText.From(
                "mod missing pub fn after -> i32 { 1 }"));
            var malformedSyntax = malformed.ParseCompilationUnit();
            Assert.That(ContainsCode(malformed.Diagnostics, "SBK1025"), Is.True);
            Assert.That(malformedSyntax.Members.Count, Is.GreaterThan(1));

            var nested = new SobakasuParser(SourceText.From(
                "fn run { mod child; pub mod public_child; } pub fn after -> i32 { 1 }"));
            nested.ParseCompilationUnit();
            Assert.That(
                nested.Diagnostics.Diagnostics.Count(
                    diagnostic => diagnostic.Code == "SBK1026"),
                Is.EqualTo(2));
        }
    }
}
