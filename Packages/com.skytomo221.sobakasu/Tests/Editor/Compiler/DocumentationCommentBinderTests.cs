using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class DocumentationCommentBinderTests
    {
        [Test]
        public void Binder_PreservesDocumentationAndSourceSpansOnDeclarationSymbols()
        {
            const string source = @"/// Type.
type External = extern UnityEngine.GameObject;
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
function `function`() {}
/// Extern function.
function external(message: string) = extern UnityEngine.Debug.Log(message)
implementation Struct {
    /// Receiver method.
    function receiver(self) {}
    /// Associated function.
    function associated() {}
}
/// Constant.
const constant: i32 = 1;
state {
    /// Private state.
    private_state: i32 = 0;
    /// Public state.
    public public_state: i32 = field;
    /// Sync state.
    public sync sync_state: i32 = field;
}
behavior {
    /// Event.
    on start {}
    /// Receive.
    receive ping() {}
}";
            var (binder, program) = Bind(source);

            Assert.That(binder.Diagnostics.HasErrors, Is.False,
                Format(binder.Diagnostics.Diagnostics));
            AssertDocumentation(FindDeclaration<TypeSymbol>(binder, "External"), "Type.", source);
            var structure = FindDeclaration<TypeSymbol>(binder, "Struct");
            AssertDocumentation(structure, "Struct.", source);
            AssertDocumentation(structure.AggregateFields[0], "Field.", source);
            var enumeration = FindDeclaration<TypeSymbol>(binder, "Enum");
            AssertDocumentation(enumeration, "Enum.", source);
            AssertDocumentation(enumeration.EnumVariants[0], "Variant.", source);
            AssertDocumentation(enumeration.EnumVariants[0].Fields[0],
                "Payload field.", source);
            AssertDocumentation(program.Functions.Single(function =>
                function.FunctionSymbol.Name == "function").FunctionSymbol,
                "Function.", source);
            AssertDocumentation(program.Functions.Single(function =>
                function.FunctionSymbol.Name == "external").FunctionSymbol,
                "Extern function.", source);
            AssertDocumentation(FindFunction(program, "receiver"),
                "Receiver method.", source);
            AssertDocumentation(FindFunction(program, "associated"),
                "Associated function.", source);
            AssertDocumentation(program.Constants[0].ConstantSymbol, "Constant.", source);
            AssertDocumentation(program.States[0].StateSymbol, "Private state.", source);
            AssertDocumentation(program.States[1].StateSymbol, "Public state.", source);
            AssertDocumentation(program.States[2].StateSymbol, "Sync state.", source);
            AssertDocumentation(program.Events[0].EventSymbol, "Event.", source);
            AssertDocumentation(program.NetworkReceivers[0].ReceiveSymbol,
                "Receive.", source);
        }

        [Test]
        public void Binder_PreservesDocumentationWhenConstructingGenericMetadata()
        {
            var definition = TypeSymbol.CreateAggregate(
                "Container", "Container", UserAggregateKind.Struct, false, string.Empty);
            definition.Documentation = Documentation("Generic type.", 1);
            var parameter = TypeSymbol.CreateGenericParameter(
                "T", definition, 0, definition.QualifiedName);
            definition.SetGenericParameters(new[] { parameter });
            var field = new AggregateFieldSymbol(
                "value", definition, parameter, 0, new TextSpan(2, 1))
            {
                Documentation = Documentation("Generic field.", 3)
            };
            definition.SetAggregateFields(new[] { field });

            var constructed = definition.Construct(new[] { TypeSymbol.I32 });
            AssertDocumentation(constructed, "Generic type.", 1);
            AssertDocumentation(constructed.AggregateFields[0], "Generic field.", 3);

            var enumDefinition = TypeSymbol.CreateAggregate(
                "Option", "Option", UserAggregateKind.Enum, false, string.Empty);
            enumDefinition.Documentation = Documentation("Generic enum.", 5);
            var enumParameter = TypeSymbol.CreateGenericParameter(
                "T", enumDefinition, 0, enumDefinition.QualifiedName);
            enumDefinition.SetGenericParameters(new[] { enumParameter });
            var variant = new EnumVariantSymbol(
                "Some", enumDefinition, EnumVariantKind.Tuple, 0,
                new[] { new AggregateFieldSymbol("0", enumDefinition,
                    enumParameter, 0, new TextSpan(6, 1)) }, new TextSpan(7, 1))
            {
                Documentation = Documentation("Generic variant.", 8)
            };
            enumDefinition.SetEnumVariants(new[] { variant });

            var constructedEnum = enumDefinition.Construct(new[] { TypeSymbol.I32 });
            AssertDocumentation(constructedEnum, "Generic enum.", 5);
            AssertDocumentation(constructedEnum.EnumVariants[0], "Generic variant.", 8);
        }

        [Test]
        public void Binder_PreservesDocumentationForPrivateAndPublicReceivers()
        {
            const string source = @"behavior {
    /// Private endpoint.
    receive private_ping {}
    /// Public endpoint.
    public receive public_ping {}
}";
            var (binder, program) = Bind(source);

            Assert.That(binder.Diagnostics.HasErrors, Is.False,
                Format(binder.Diagnostics.Diagnostics));
            AssertDocumentation(program.NetworkReceivers[0].ReceiveSymbol,
                "Private endpoint.", source);
            AssertDocumentation(program.NetworkReceivers[1].ReceiveSymbol,
                "Public endpoint.", source);
        }

        [Test]
        public void Binder_PreservesDocumentationOnInstantiatedGenericImplMethods()
        {
            const string source = @"/// Box.
struct Box<T> {
    value: T,
}
implementation<T> Box<T> {
    /// Get.
    function get(self) -> T { self.value }
}
behavior { on start {
    let value: Box<i32> = Box { value: 1, };
    value.get();
} }";
            var (binder, program) = Bind(source);

            Assert.That(binder.Diagnostics.HasErrors, Is.False,
                Format(binder.Diagnostics.Diagnostics));
            var method = FindFunction(program, "get");
            Assert.That(method.ContainingType.IsConstructedGenericType, Is.True);
            AssertDocumentation(method, "Get.", source);
        }

        [Test]
        public void Symbol_ModelLimitsDocumentabilityToSourceDeclarationSymbols()
        {
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(TypeSymbol)), Is.True);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(FunctionSymbol)), Is.True);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(ConstantSymbol)), Is.True);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(StateVariableSymbol)), Is.True);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(AggregateFieldSymbol)), Is.True);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(EnumVariantSymbol)), Is.True);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(BoundEventSymbol)), Is.True);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(NetworkReceiveSymbol)), Is.True);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(ParameterSymbol)), Is.False);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(LocalVariableSymbol)), Is.False);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(NamespaceSymbol)), Is.False);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(ModuleSymbol)), Is.False);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(MethodGroupSymbol)), Is.False);
            Assert.That(typeof(IDocumentableSymbol).IsAssignableFrom(typeof(FunctionGroupSymbol)), Is.False);
            Assert.That(typeof(Symbol).GetProperty("Documentation"), Is.Null);
        }

        private static (SobakasuBinder Binder, BoundProgram Program) Bind(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.HasErrors, Is.False,
                Format(parser.Diagnostics.Diagnostics));
            var binder = new SobakasuBinder(SobakasuTestEnvironment.Default);
            return (binder, binder.BindProgram(syntax));
        }

        private static T FindDeclaration<T>(SobakasuBinder binder, string name)
            where T : Symbol
        {
            foreach (var module in binder.ModuleSymbols.Values)
                if (module.LookupDeclared(name) is T symbol)
                    return symbol;
            Assert.Fail($"Declaration '{name}' was not found.");
            return null;
        }

        private static FunctionSymbol FindFunction(BoundProgram program, string name)
        {
            return program.Functions.Single(function =>
                function.FunctionSymbol.Name == name).FunctionSymbol;
        }

        private static DocumentationComment Documentation(string markdown, int start)
        {
            return new DocumentationComment(markdown,
                new TextSpan(start, ("/// " + markdown).Length));
        }

        private static void AssertDocumentation(
            IDocumentableSymbol symbol,
            string markdown,
            string source)
        {
            Assert.That(symbol.Documentation, Is.Not.Null);
            Assert.That(symbol.Documentation.Markdown, Is.EqualTo(markdown));
            var start = source.IndexOf("/// " + markdown, StringComparison.Ordinal);
            AssertDocumentation(symbol, markdown, start);
        }

        private static void AssertDocumentation(
            IDocumentableSymbol symbol,
            string markdown,
            int start)
        {
            Assert.That(symbol.Documentation, Is.Not.Null);
            Assert.That(symbol.Documentation.Markdown, Is.EqualTo(markdown));
            Assert.That(symbol.Documentation.SourceSpan.Start, Is.EqualTo(start));
            Assert.That(symbol.Documentation.SourceSpan.Length,
                Is.EqualTo(("/// " + markdown).Length));
        }

        private static string Format(IReadOnlyList<Diagnostic> diagnostics)
        {
            return string.Join("\n", diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}"));
        }
    }
}
