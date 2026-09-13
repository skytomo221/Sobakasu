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


    }
}
