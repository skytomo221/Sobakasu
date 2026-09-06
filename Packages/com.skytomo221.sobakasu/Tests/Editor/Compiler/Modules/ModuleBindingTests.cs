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
    public class ModuleBindingTests
    {

        [Test]
        public void Binder_PreservesDeclarationIdentityAndCanonicalPublicPath()
        {
            WithTemporaryLibrary(root =>
            {
                WriteHierarchy(root, includePrelude: false);
                var resolution = new StandardLibraryResolver().Resolve("use api.twice;", root);
                var binder = new Skytomo221.Sobakasu.Compiler.Binder.SobakasuBinder();
                binder.BindProgram(resolution.Graph);

                Assert.That(resolution.Diagnostics.HasErrors, Is.False);
                Assert.That(binder.Diagnostics.HasErrors, Is.False);
                var api = resolution.Graph.FindModule("api");
                var child = resolution.Graph.FindModule("api.private_child");
                var fromParent = binder.ModuleSymbols[api].LookupExport("twice");
                var fromChild = binder.ModuleSymbols[child].LookupExport("twice");
                var parentGroup = (Skytomo221.Sobakasu.Compiler.Binder.FunctionGroupSymbol)fromParent;
                var childGroup = (Skytomo221.Sobakasu.Compiler.Binder.FunctionGroupSymbol)fromChild;
                Assert.That(parentGroup.Functions, Has.Count.EqualTo(1));
                Assert.That(childGroup.Functions, Has.Count.EqualTo(1));
                Assert.That(parentGroup.Functions[0], Is.SameAs(childGroup.Functions[0]));
                var function = parentGroup.Functions[0];
                Assert.That(function.DeclarationIdentity,
                    Is.EqualTo("api.private_child.twice"));
                Assert.That(function.CanonicalPublicPath, Is.EqualTo("api.twice"));
            });
        }
    }
}
