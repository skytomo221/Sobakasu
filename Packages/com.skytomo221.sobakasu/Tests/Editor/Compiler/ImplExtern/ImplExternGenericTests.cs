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
using Skytomo221.Sobakasu.Compiler.Target;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;

using static Skytomo221.Sobakasu.Tests.Editor.ImplExternTestSupport;
namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class ImplExternGenericTests
    {
        [Test]
        public void Parser_ParsesGenericFunctionAndCallableApplications()
        {
            var parser = new SobakasuParser(SourceText.From(@"
function foo<T, U>() -> T = extern Test.Api.Foo<T, U>();
behavior { on start {
  foo<i32, string>();
  receiver.foo<string>();
} }"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var function = (FunctionDeclarationSyntax)syntax.Members[0];
            Assert.That(function.GenericParameters.Parameters.Select(token => token.Text),
                Is.EqualTo(new[] { "T", "U" }));
            var firstCall = (ExpressionStatementSyntax)((EventDeclarationSyntax)
                ((BehaviorDeclarationSyntax)syntax.Members[1]).Members[0]).Body.Statements[0];
            var call = (CallExpressionSyntax)firstCall.Expression;
            Assert.That(call.Target, Is.TypeOf<GenericTypeExpressionSyntax>());
            Assert.That(((GenericTypeExpressionSyntax)call.Target)
                .TypeArgumentList.Arguments, Has.Count.EqualTo(2));
        }

        [Test]
        public void TypeSymbol_ConstructsAndSubstitutesGenericExternTypesRecursively()
        {
            var catalog = CreateGenericExternEnvironment().ExternCatalog;

            Assert.That(catalog.TryGetTypeSymbol("System.Collections.Generic.List`1", out var list), Is.True);
            Assert.That(list.IsGenericDefinition, Is.True);
            var constructed = list.Construct(new[] { TypeSymbol.String });
            Assert.That(constructed.IsExternalBinding, Is.True);
            Assert.That(constructed.GenericDefinition, Is.SameAs(list));
            Assert.That(constructed.TypeArguments, Is.EqualTo(new[] { TypeSymbol.String }));
            Assert.That(catalog.TryGetRuntimeTypeIdentity(constructed, out var runtimeType), Is.True);
            Assert.That(runtimeType.GenericDefinition.RuntimeName,
                Is.EqualTo("System.Collections.Generic.List`1"));
            Assert.That(runtimeType.TypeArguments[0].RuntimeName, Is.EqualTo("System.String"));

            var stringBinding = TypeSymbol.CreateExternalBinding(
                "StringBinding",
                "sample.StringBinding",
                TypeSymbol.String,
                true,
                "sample");
            var equivalent = list.Construct(new[] { stringBinding });
            Assert.That(equivalent, Is.Not.SameAs(constructed));
            Assert.That(catalog.GetRuntimeTypeSymbol(equivalent), Is.SameAs(constructed));

            var parameter = list.GenericParameters[0];
            var nested = list.Construct(new[] { TypeSymbol.Array(parameter) });
            var substituted = TypeSymbol.Substitute(nested,
                new Dictionary<TypeSymbol, TypeSymbol>
                {
                    [parameter] = TypeSymbol.String
                });
            Assert.That(substituted.TypeArguments[0],
                Is.SameAs(TypeSymbol.Array(TypeSymbol.String)));

            Assert.That(catalog.TryGetTypeSymbol(
                "Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture", out var fixtureType), Is.True);
            var baseConstraint = catalog.GetExternalMethodGroup(fixtureType, "BaseConstraint")
                .Methods.Cast<ExternMethodSymbol>().Single();
            Assert.That(baseConstraint.GenericConstraints[0].ConstraintTypes[0]
                .RuntimeQualifiedName,
                Is.EqualTo("Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericConstraintBase"));
            var interfaceConstraint = catalog.GetExternalMethodGroup(fixtureType, "InterfaceConstraint")
                .Methods.Cast<ExternMethodSymbol>().Single();
            Assert.That(interfaceConstraint.GenericConstraints[0].ConstraintTypes[0]
                .RuntimeQualifiedName,
                Is.EqualTo("Skytomo221.Sobakasu.Tests.Editor.ISobakasuGenericConstraint"));
            var structConstraint = catalog.GetExternalMethodGroup(fixtureType, "StructConstraint")
                .Methods.Cast<ExternMethodSymbol>().Single();
            Assert.That(structConstraint.GenericConstraints[0].RequiresNonNullableValueType, Is.True);
            var constructorConstraint = catalog.GetExternalMethodGroup(fixtureType, "ConstructorConstraint")
                .Methods.Cast<ExternMethodSymbol>().Single();
            Assert.That(constructorConstraint.GenericConstraints[0].RequiresDefaultConstructor, Is.True);
        }

        [Test]
        public void ReflectionCatalog_DoesNotEagerlyExpandOpenGenericDeclaringTypes()
        {
            var catalog = CreateGenericExternEnvironment().ExternCatalog;

            Assert.That(catalog.TryGetTypeSymbol(
                "Skytomo221.Sobakasu.Tests.Editor.SobakasuUnusedGenericExternFixture`1", out _), Is.False);
        }

        [Test]
        public void RuntimeValueKeyFormatter_FormatsSymbolicValuesWithoutClrMaterialization()
        {
            var missingType = RuntimeTypeIdentity.Named("Missing.Assembly.Type");
            var genericType = RuntimeTypeIdentity.ConstructedGeneric(
                RuntimeTypeIdentity.Named("Missing.Generic`1"),
                new[] { missingType });
            var arrayType = RuntimeTypeIdentity.Array(genericType);
            var enumType = RuntimeTypeIdentity.Named("Missing.Enum");
            var value = new RuntimeArrayConstantValue(
                arrayType,
                new object[]
                {
                    new RuntimeEnumConstantValue(enumType, "Value", "7"),
                    "value:with;delimiters",
                    null
                });
            var equivalent = new RuntimeArrayConstantValue(
                RuntimeTypeIdentity.Array(
                    RuntimeTypeIdentity.ConstructedGeneric(
                        RuntimeTypeIdentity.Named("Missing.Generic`1"),
                        new[] { RuntimeTypeIdentity.Named("Missing.Assembly.Type") })),
                new object[]
                {
                    new RuntimeEnumConstantValue(
                        RuntimeTypeIdentity.Named("Missing.Enum"),
                        "AliasWithSameRuntimeValue",
                        "7"),
                    "value:with;delimiters",
                    null
                });
            var different = new RuntimeArrayConstantValue(
                arrayType,
                new object[]
                {
                    new RuntimeEnumConstantValue(enumType, "Value", "8"),
                    "value:with;delimiters",
                    null
                });

            var key = RuntimeValueKeyFormatter.Format(value);

            Assert.That(key, Does.Contain("Missing.Assembly.Type"));
            Assert.That(key, Is.EqualTo(RuntimeValueKeyFormatter.Format(equivalent)));
            Assert.That(key, Is.Not.EqualTo(RuntimeValueKeyFormatter.Format(different)));
        }

        [Test]
        public void GenericExtern_LowersHiddenSystemTypeAndKeepsOpenSignature()
        {
            var environment = CreateGenericExternEnvironment();
            const string signature =
                "Skytomo221SobakasuTestsEditorSobakasuGenericExternFixture.__Echo__SystemType_T__T";
            var (_, Ir, Uasm) = CompileWithEnvironment(@"
public implementation GenericApi = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture {
  public function echo<T>(self, value: T) -> T = extern self.Echo<T>(value)
}
behavior { on start {
  let api = extern new Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture();
  let value = api.echo<string>(""ok"");
} }", environment);

            var call = FindExternCall(Ir, signature);
            Assert.That(call.Arguments, Has.Count.EqualTo(3));
            Assert.That(call.Arguments[1], Is.TypeOf<IrConstantValue>());
            Assert.That(((IrConstantValue)call.Arguments[1]).Value,
                Is.TypeOf<RuntimeTypeIdentity>());
            Assert.That(
                ((RuntimeTypeIdentity)((IrConstantValue)call.Arguments[1]).Value)
                    .RuntimeName,
                Is.EqualTo("System.String"));
            Assert.That(Uasm, Does.Contain($"EXTERN, \"{signature}\""));
            Assert.That(signature, Does.Contain("__T"));
        }

        [Test]
        public void GenericExtern_ReportsClrConstraintViolationInBinder()
        {
            var binder = Bind(@"
public implementation GenericApi = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture {
  public function echo<T>(self, value: T) -> T = extern self.Echo<T>(value)
}
behavior { on start {
  let api = extern new Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture();
  let value = api.echo<i32>(1);
} }", CreateGenericExternEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "SBK2126"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }
    }
}
