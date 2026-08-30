using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Lexer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuLanguageItemTests
    {
        [Test]
        public void Parser_ParsesLanguageItemsOnSupportedTypeDeclarations()
        {
            var parser = new SobakasuParser(SourceText.From(@"
lang ""maybe""
pub enum Optional<T> { Nothing, Just(T), }
lang ""network_event_target""
pub impl NetTarget = extern VRC.Udon.Common.Interfaces.NetworkEventTarget {}
lang ""maybe""
struct Placeholder {}
"));

            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);
            Assert.That(((EnumDeclarationSyntax)syntax.Members[0]).LanguageItem.Item.Value,
                Is.EqualTo("maybe"));
            Assert.That(((ImplDeclarationSyntax)syntax.Members[1]).LanguageItem.Item.Value,
                Is.EqualTo("network_event_target"));
            Assert.That(((StructDeclarationSyntax)syntax.Members[2]).LanguageItem.Item.Value,
                Is.EqualTo("maybe"));
        }

        [Test]
        public void Parser_ReportsInvalidTargetAndRecoversAfterMalformedLanguageItem()
        {
            var invalidTarget = new SobakasuParser(SourceText.From(
                "lang \"maybe\" fn value() {}"));
            var invalidSyntax = invalidTarget.ParseCompilationUnit();

            Assert.That(invalidSyntax.Members[0], Is.TypeOf<FunctionDeclarationSyntax>());
            Assert.That(ContainsCode(invalidTarget, "SBK1042"), Is.True);

            var malformed = new SobakasuParser(SourceText.From(@"
lang pub struct Broken {}
lang ""maybe""
enum Optional<T> { Nothing, Just(T), }
"));
            var recovered = malformed.ParseCompilationUnit();

            Assert.That(ContainsCode(malformed, "SBK1001"), Is.True);
            Assert.That(recovered.Members, Has.Count.EqualTo(2));
            Assert.That(recovered.Members[1], Is.TypeOf<EnumDeclarationSyntax>());
        }

        [Test]
        public void Binder_RegistersMaybeBySemanticIdentityAfterRename()
        {
            var binder = Bind(@"
lang ""maybe""
enum Optional<T> { Nothing, Just(T), }
pub impl ObjectRef = extern UnityEngine.GameObject {}
fn find(name: string) -> Optional<ObjectRef>
  = maybe extern UnityEngine.GameObject.Find(name)
on start { let value = find(""target""); }
", out _);

            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(binder));
            Assert.That(binder.LanguageItems[LanguageItemNames.Maybe].Name,
                Is.EqualTo("Optional"));
        }

        [Test]
        public void Binder_UsesRenamedNetworkEventTargetLanguageItem()
        {
            var binder = Bind(@"
lang ""network_event_target""
pub impl NetTarget = extern VRC.Udon.Common.Interfaces.NetworkEventTarget {
  pub static fn All -> Self { extern Self.All }
}
fn target -> NetTarget { NetTarget.All }
receive ping {}
on interact {
  send ping to all;
  send ping to target();
}
", out var program);

            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                FormatDiagnostics(binder));
            var targetType = binder.LanguageItems[LanguageItemNames.NetworkEventTarget];
            Assert.That(targetType.Name, Is.EqualTo("NetTarget"));
            Assert.That(((BoundNetworkSendStatement)program.Events[0].Body.Statements[0])
                .Target.Type, Is.SameAs(targetType));
            Assert.That(((BoundNetworkSendStatement)program.Events[0].Body.Statements[1])
                .Target.Type, Is.SameAs(targetType));
        }

        [TestCase(@"lang ""mabye"" struct Value {}", "SBK2165")]
        [TestCase(@"lang ""maybe"" struct First {} lang ""maybe"" struct Second {}", "SBK2166")]
        [TestCase(@"lang ""maybe"" impl i32 {}", "SBK2167")]
        public void Binder_ReportsLanguageItemDiagnostics(string source, string code)
        {
            var binder = Bind(source, out _);

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                diagnostic.Code == code), Is.True, FormatDiagnostics(binder));
        }

        private static SobakasuBinder Bind(string source, out BoundProgram program)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                string.Join("\n", parser.Diagnostics.Diagnostics));
            var binder = new SobakasuBinder();
            program = binder.BindProgram(syntax);
            return binder;
        }

        private static bool ContainsCode(SobakasuParser parser, string code)
        {
            return parser.Diagnostics.Diagnostics.Any(diagnostic => diagnostic.Code == code);
        }

        private static string FormatDiagnostics(SobakasuBinder binder)
        {
            return string.Join("\n", binder.Diagnostics.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}"));
        }
    }
}
