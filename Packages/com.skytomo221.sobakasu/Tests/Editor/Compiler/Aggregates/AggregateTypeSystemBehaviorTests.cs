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
    public class AggregateTypeSystemBehaviorTests : AggregateTestFixture
    {


        [Test]
        public void TypeSymbol_InternsStructuralTuplesAndKeepsOneTupleDistinct()
        {
            var first = TypeSymbol.Tuple(new[] { TypeSymbol.I32, TypeSymbol.String });
            var second = TypeSymbol.Tuple(new[] { TypeSymbol.I32, TypeSymbol.String });
            var one = TypeSymbol.Tuple(new[] { TypeSymbol.I32 });
            var oneUnit = TypeSymbol.Tuple(new[] { TypeSymbol.Unit });

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.Name, Is.EqualTo("(i32, string)"));
            Assert.That(first.AggregateFields[0].Name, Is.EqualTo("0"));
            Assert.That(first.AggregateFields[1].Type, Is.SameAs(TypeSymbol.String));
            Assert.That(TypeSymbol.Tuple(Array.Empty<TypeSymbol>()), Is.SameAs(TypeSymbol.Unit));
            Assert.That(one.Name, Is.EqualTo("(i32,)"));
            Assert.That(one, Is.Not.EqualTo(TypeSymbol.I32));
            Assert.That(oneUnit.Name, Is.EqualTo("((),)"));
            Assert.That(oneUnit, Is.Not.EqualTo(TypeSymbol.Unit));
        }

        [Test]
        public void TypeSymbol_DistinguishesExternalAggregateKindFromFlattenedStorage()
        {
            var runtime = TypeSymbol.CreateNamed("Vector3", "UnityEngine.Vector3", false);
            var external = TypeSymbol.CreateExternalAggregateBinding(
                "Vector", "Vector", runtime, UserAggregateKind.Struct, true, string.Empty);

            Assert.That(external.IsAggregate, Is.True);
            Assert.That(external.IsExternalBinding, Is.True);
            Assert.That(external.UsesFlattenedAggregateStorage, Is.False);
            Assert.That(TypeSymbol.Array(external).ElementType.UsesFlattenedAggregateStorage, Is.False);
        }


    }
}
