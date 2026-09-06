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
    public class ImplExternGenericTests
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

        private void RegisterForCleanup(string assetPath)
        {
            if (!string.IsNullOrWhiteSpace(assetPath))
                _cleanupAssetPaths.Add(assetPath);
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

        [Test]
        public void TypeSymbol_ConstructsAndSubstitutesGenericExternTypesRecursively()
        {
            var signatures = typeof(SobakasuGenericExternFixture)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.DeclaringType == typeof(SobakasuGenericExternFixture))
                .Select(UdonExternSignatureFormatter.GetUdonMethodName)
                .ToArray();
            var catalog = new ReflectionExternCatalogBuilder(
                new UdonExposedNodeCache(signatures))
                .BuildCatalog(new[]
                {
                    typeof(SobakasuGenericExternFixture).Namespace,
                    typeof(List<>).Namespace
                });

            Assert.That(catalog.TryGetTypeSymbol(typeof(List<>), out var list), Is.True);
            Assert.That(list.IsGenericDefinition, Is.True);
            var constructed = list.Construct(new[] { TypeSymbol.String });
            Assert.That(constructed.IsExternalBinding, Is.True);
            Assert.That(constructed.GenericDefinition, Is.SameAs(list));
            Assert.That(constructed.TypeArguments, Is.EqualTo(new[] { TypeSymbol.String }));
            Assert.That(catalog.TryGetClrType(constructed, out var runtimeType), Is.True);
            Assert.That(runtimeType, Is.EqualTo(typeof(List<string>)));

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
                typeof(SobakasuGenericExternFixture), out var fixtureType), Is.True);
            var baseConstraint = fixtureType.GetMethodGroup("BaseConstraint")
                .Methods.Cast<ExternMethodSymbol>().Single();
            Assert.That(baseConstraint.GenericConstraints[0].ConstraintTypes[0]
                .RuntimeClrType, Is.EqualTo(typeof(SobakasuGenericConstraintBase)));
            var interfaceConstraint = fixtureType.GetMethodGroup("InterfaceConstraint")
                .Methods.Cast<ExternMethodSymbol>().Single();
            Assert.That(interfaceConstraint.GenericConstraints[0].ConstraintTypes[0]
                .RuntimeClrType, Is.EqualTo(typeof(ISobakasuGenericConstraint)));
            var structConstraint = fixtureType.GetMethodGroup("StructConstraint")
                .Methods.Cast<ExternMethodSymbol>().Single();
            Assert.That(structConstraint.GenericConstraints[0].Attributes &
                GenericParameterAttributes.NotNullableValueTypeConstraint,
                Is.Not.EqualTo(0));
            var constructorConstraint = fixtureType.GetMethodGroup("ConstructorConstraint")
                .Methods.Cast<ExternMethodSymbol>().Single();
            Assert.That(constructorConstraint.GenericConstraints[0].Attributes &
                GenericParameterAttributes.DefaultConstructorConstraint,
                Is.Not.EqualTo(0));
        }

        [Test]
        public void ReflectionCatalog_DoesNotEagerlyExpandOpenGenericDeclaringTypes()
        {
            var catalog = new ReflectionExternCatalogBuilder(
                new UdonExposedNodeCache(Array.Empty<string>()))
                .BuildCatalog(new[] { typeof(SobakasuUnusedGenericExternFixture<>).Namespace });

            Assert.That(catalog.TryGetTypeSymbol(
                typeof(SobakasuUnusedGenericExternFixture<>), out _), Is.False);
        }

        [Test]
        public void HeapPatchValueSerializer_RoundTripsSystemTypeIdentity()
        {
            var serialized = HeapPatchValueSerializer.SerializeRuntimeValue(
                typeof(SobakasuGenericExternFixture),
                TypeKind.Named,
                typeof(Type).FullName);
            var restored = HeapPatchValueSerializer.DeserializeRuntimeValue(
                serialized,
                TypeKind.Named,
                typeof(Type).FullName);

            Assert.That(restored, Is.EqualTo(typeof(SobakasuGenericExternFixture)));
            Assert.That(serialized, Does.Contain(
                typeof(SobakasuGenericExternFixture).FullName));
        }

        [Test]
        public void GenericExtern_LowersHiddenSystemTypeAndKeepsOpenSignature()
        {
            var environment = CreateGenericExternEnvironment();
            var signature = UdonExternSignatureFormatter.GetUdonMethodName(
                typeof(SobakasuGenericExternFixture).GetMethod("Echo"));
            var (Program, Ir, Uasm) = CompileWithEnvironment(@"
pub impl GenericApi = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture {
  pub fn echo<T>(value: T) -> T = extern self.Echo<T>(value)
}
on start {
  let api = extern new Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture();
  let value = api.echo<string>(""ok"");
}", environment);

            var call = FindExternCall(Ir, signature);
            Assert.That(call.Arguments, Has.Count.EqualTo(3));
            Assert.That(call.Arguments[1], Is.TypeOf<IrConstantValue>());
            Assert.That(((IrConstantValue)call.Arguments[1]).Value,
                Is.EqualTo(typeof(string)));
            Assert.That(Uasm, Does.Contain($"EXTERN, \"{signature}\""));
            Assert.That(signature, Does.Contain("__T"));
        }

        [Test]
        public void GenericExtern_ReportsClrConstraintViolationInBinder()
        {
            var binder = Bind(@"
pub impl GenericApi = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture {
  pub fn echo<T>(value: T) -> T = extern self.Echo<T>(value)
}
on start {
  let api = extern new Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture();
  let value = api.echo<i32>(1);
}", CreateGenericExternEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                diagnostic.Code == "SBK2126"), Is.True,
                Format(binder.Diagnostics.Diagnostics));
        }
    }
}
