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
    public class ImplExternTypeSystemBehaviorTests : ImplExternTestFixture
    {


        [Test]
        public void ReflectionCatalog_DefaultIncludesUdonExposedTypesOutsideExplicitNamespaceScope()
        {
            var catalog = CreateExternAbiEnvironment().ExternCatalog;

            Assert.That(catalog.TryGetTypeSymbol(
                "Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture", out _),
                Is.True);
            Assert.That(catalog.TryGetTypeSymbol(
                "Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture", out _),
                Is.False);
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


    }
}
