using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal sealed class UdonApiCatalogLoaderTests
    {
        [Test]
        public void Load_NormalizesConstructorToSobakasuNew()
        {
            var catalog = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(CreateConstructorCatalog()));

            Assert.That(catalog.TryGetTypeSymbol("Test.Value", out var type), Is.True);

            var group = catalog.GetExternalMethodGroup(type, "new");
            Assert.That(group, Is.Not.Null);
            Assert.That(group.Methods, Has.Count.EqualTo(1));

            var constructor = group.Methods[0] as ExternMethodSymbol;
            Assert.That(constructor, Is.Not.Null);
            Assert.That(constructor.Name, Is.EqualTo("new"));
            Assert.That(constructor.MemberKind, Is.EqualTo(ExternMemberKind.Constructor));
            Assert.That(constructor.IsStatic, Is.True);
            Assert.That(constructor.Parameters, Has.Count.EqualTo(1));
            Assert.That(constructor.Parameters[0].Type, Is.EqualTo(TypeSymbol.I32));
            Assert.That(constructor.AbiParameters, Has.Count.EqualTo(1));
            Assert.That(constructor.AbiParameters[0].LogicalInputOrdinal, Is.EqualTo(0));
            Assert.That(constructor.AbiReturnType, Is.EqualTo(type));

            Assert.That(catalog.GetExternalMethodGroup(type, ".ctor"), Is.Null);
        }

        [Test]
        public void Load_NormalizesUnexposedConstructorToSobakasuNew()
        {
            var data = CreateConstructorCatalog();
            data.unexposedMembers.Add(new UdonApiUnexposedMemberRecord
            {
                hostType = "Test.Value",
                name = ".ctor",
                kind = "Constructor",
                externSignature = "TestValue.__ctor__SystemInt32__TestValue"
            });
            data.members.Clear();

            var catalog = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(data));

            Assert.That(catalog.TryGetTypeSymbol("Test.Value", out var type), Is.True);

            var group = catalog.GetExternalMethodGroup(type, "new");
            Assert.That(group, Is.Not.Null);
            Assert.That(group.Methods, Is.Empty);
            Assert.That(group.RejectedCandidates, Has.Count.EqualTo(1));
            Assert.That(catalog.GetExternalMethodGroup(type, ".ctor"), Is.Null);
        }

        [Test]
        public void Load_PromotesLegacyBooleanLogicalNotUnmatchedSignature()
        {
            const string signature =
                "SystemBoolean.__op_UnaryNegation__SystemBoolean__SystemBoolean";
            var data = new UdonApiCatalogData
            {
                types = new List<UdonApiTypeRecord>
                {
                    new()
                    {
                        runtimeName = "System.Boolean",
                        shape = "Value"
                    }
                },
                unmatchedUdonSignatures = new List<string> { signature }
            };

            var catalog = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(data));

            var signatures = catalog.GetUnaryOperatorSignatures(
                "op_LogicalNot",
                TypeSymbol.Bool,
                TypeSymbol.Bool);
            Assert.That(signatures, Is.EqualTo(new[] { signature }));
        }

        [Test]
        public void Load_Twice_DoesNotAccumulateBuiltInExternalMembers()
        {
            var before = TypeSymbol.Object.GetMethodGroup("Equals")?.Methods.Count ?? 0;
            var data = CreateObjectMethodCatalog();

            var first = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(data));
            var second = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(data));

            Assert.That(first.TryGetTypeSymbol("System.Object", out var firstObject), Is.True);
            Assert.That(second.TryGetTypeSymbol("System.Object", out var secondObject), Is.True);

            var firstGroup = first.GetExternalMethodGroup(firstObject, "Equals");
            var secondGroup = second.GetExternalMethodGroup(secondObject, "Equals");
            Assert.That(firstGroup, Is.Not.Null);
            Assert.That(secondGroup, Is.Not.Null);
            Assert.That(firstGroup.Methods, Has.Count.EqualTo(1));
            Assert.That(secondGroup.Methods, Has.Count.EqualTo(1));
            Assert.That(secondGroup, Is.Not.SameAs(firstGroup));

            var after = TypeSymbol.Object.GetMethodGroup("Equals")?.Methods.Count ?? 0;
            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void Load_DifferentCatalogs_DoNotShareExternalMemberState()
        {
            var withMethod = CreateObjectMethodCatalog();
            var withoutMethod = CreateObjectMethodCatalog();
            withoutMethod.members.Clear();

            var first = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(withMethod));
            var second = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(withoutMethod));

            Assert.That(first.TryGetTypeSymbol("System.Object", out var firstObject), Is.True);
            Assert.That(second.TryGetTypeSymbol("System.Object", out var secondObject), Is.True);
            Assert.That(first.GetExternalMethodGroup(firstObject, "Equals"), Is.Not.Null);
            Assert.That(second.GetExternalMethodGroup(secondObject, "Equals"), Is.Null);
        }

        [Test]
        public void Load_DifferentCatalogs_DoNotShareRejectedExternalCandidates()
        {
            var withRejectedCandidate = CreateObjectMethodCatalog();
            withRejectedCandidate.members.Clear();
            withRejectedCandidate.unexposedMembers.Add(new UdonApiUnexposedMemberRecord
            {
                hostType = "System.Object",
                name = "Hidden",
                kind = "Method",
                externSignature = "SystemObject.__Hidden__SystemVoid"
            });
            var withoutRejectedCandidate = CreateObjectMethodCatalog();
            withoutRejectedCandidate.members.Clear();

            var first = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(withRejectedCandidate));
            var second = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(withoutRejectedCandidate));

            Assert.That(first.TryGetTypeSymbol("System.Object", out var firstObject), Is.True);
            Assert.That(second.TryGetTypeSymbol("System.Object", out var secondObject), Is.True);
            var group = first.GetExternalMethodGroup(firstObject, "Hidden");
            Assert.That(group, Is.Not.Null);
            Assert.That(group.Methods, Is.Empty);
            Assert.That(group.RejectedCandidates, Has.Count.EqualTo(1));
            Assert.That(second.GetExternalMethodGroup(secondObject, "Hidden"), Is.Null);
        }

        [Test]
        public void Load_IndexesGenericTypeBySourceNameAndArity()
        {
            var data = new UdonApiCatalogData
            {
                types = new List<UdonApiTypeRecord>
                {
                    new()
                    {
                        runtimeName = "System.Collections.Generic.List`1",
                        shape = "Reference",
                        genericArity = 1
                    }
                }
            };

            var catalog = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(data));

            Assert.That(catalog.TryGetSourceTypeSymbol(
                "System.Collections.Generic.List", 1, out var list), Is.True);
            Assert.That(list.IsGenericDefinition, Is.True);
            Assert.That(list.GenericParameters, Has.Count.EqualTo(1));
            Assert.That(catalog.TryGetTypeSymbol(
                "System.Collections.Generic.List`1", out var runtimeType), Is.True);
            Assert.That(runtimeType, Is.SameAs(list));
        }

        [Test]
        public void Load_DistinguishesSourceTypesByGenericArity()
        {
            var data = new UdonApiCatalogData
            {
                types = new List<UdonApiTypeRecord>
                {
                    new()
                    {
                        runtimeName = "Test.Foo",
                        shape = "Reference"
                    },
                    new()
                    {
                        runtimeName = "Test.Foo`1",
                        shape = "Reference",
                        genericArity = 1
                    },
                    new()
                    {
                        runtimeName = "Test.Foo`2",
                        shape = "Reference",
                        genericArity = 2
                    }
                }
            };

            var catalog = UdonApiCatalogLoader.Load(
                UdonApiCatalogGenerator.Serialize(data));

            Assert.That(catalog.TryGetSourceTypeSymbol(
                "Test.Foo", 0, out var arityZero), Is.True);
            Assert.That(catalog.TryGetSourceTypeSymbol(
                "Test.Foo", 1, out var arityOne), Is.True);
            Assert.That(catalog.TryGetSourceTypeSymbol(
                "Test.Foo", 2, out var arityTwo), Is.True);
            Assert.That(arityZero, Is.Not.SameAs(arityOne));
            Assert.That(arityOne, Is.Not.SameAs(arityTwo));
            Assert.That(catalog.TryGetTypeSymbol("Test.Foo`2", out var runtimeType),
                Is.True);
            Assert.That(runtimeType, Is.SameAs(arityTwo));
        }

        [Test]
        public void SourceTypeName_NormalizesNestedClrGenericSegments()
        {
            Assert.That(ExternCatalog.GetSourceQualifiedName(
                    "Test.Outer`1+Inner`1"),
                Is.EqualTo("Test.Outer.Inner"));
        }

        [Test]
        public void CatalogGenericType_ResolvesFromSourceSyntax()
        {
            var binder = ImplExternTestSupport.Bind(@"
fn accepts<T>(values: System::Collections::Generic::List<T>) {}
", CreateCatalogGenericEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "SBK2015"),
                Is.False,
                ImplExternTestSupport.Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void CatalogGenericExtern_DeferredForwardingBindsExternalWrapper()
        {
            var binder = ImplExternTestSupport.Bind(@"
pub fn get<T>() -> T = extern Test.Host.Get<T>()
", CreateCatalogGenericEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "SBK2126"),
                Is.False,
                ImplExternTestSupport.Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void CatalogGenericExtern_ValidConcreteArgumentSatisfiesConstraint()
        {
            var binder = ImplExternTestSupport.Bind(@"
pub fn get<T>() -> T = extern Test.Host.Get<T>()
on start {
  let value = get<Test::Transform>();
}
", CreateCatalogGenericEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                ImplExternTestSupport.Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void CatalogGenericExtern_InvalidConcreteArgumentReportsConstraintViolation()
        {
            var binder = ImplExternTestSupport.Bind(@"
pub fn get<T>() -> T = extern Test.Host.Get<T>()
on start {
  let value = get<i32>();
}
", CreateCatalogGenericEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "SBK2126"),
                Is.True,
                ImplExternTestSupport.Format(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void CatalogGenericExtern_OrdinaryGenericFunctionDoesNotDeferConstraints()
        {
            var binder = ImplExternTestSupport.Bind(@"
fn get<T>() -> T {
  return extern Test.Host.Get<T>();
}
", CreateCatalogGenericEnvironment());

            Assert.That(binder.Diagnostics.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "SBK2126"),
                Is.True,
                ImplExternTestSupport.Format(binder.Diagnostics.Diagnostics));
        }

        private static UdonApiCatalogData CreateConstructorCatalog()
        {
            return new UdonApiCatalogData
            {
                types = new List<UdonApiTypeRecord>
                {
                    new()
                    {
                        runtimeName = "System.Int32",
                        shape = "Value"
                    },
                    new()
                    {
                        runtimeName = "Test.Value",
                        shape = "Value"
                    }
                },
                members = new List<UdonApiMemberRecord>
                {
                    new()
                    {
                        hostType = Named("Test.Value"),
                        clrDeclaringType = Named("Test.Value"),
                        name = ".ctor",
                        kind = "Constructor",
                        origin = "Clr",
                        isStatic = false,
                        externSignature = "TestValue.__ctor__SystemInt32__TestValue",
                        abiParameters = new List<ExternParameterRecord>
                        {
                            new()
                            {
                                name = "value",
                                type = Named("System.Int32"),
                                passingMode = "Normal"
                            }
                        },
                        abiReturnType = Named("Test.Value")
                    }
                }
            };
        }

        private static UdonApiCatalogData CreateObjectMethodCatalog()
        {
            return new UdonApiCatalogData
            {
                types = new List<UdonApiTypeRecord>
                {
                    new()
                    {
                        runtimeName = "System.Object",
                        shape = "Reference"
                    },
                    new()
                    {
                        runtimeName = "System.Boolean",
                        shape = "Value"
                    }
                },
                members = new List<UdonApiMemberRecord>
                {
                    new()
                    {
                        hostType = Named("System.Object"),
                        clrDeclaringType = Named("System.Object"),
                        name = "Equals",
                        kind = "Method",
                        origin = "Clr",
                        isStatic = true,
                        externSignature = "SystemObject.__Equals__SystemObject_SystemObject__SystemBoolean",
                        abiParameters = new List<ExternParameterRecord>
                        {
                            new()
                            {
                                name = "objA",
                                type = Named("System.Object"),
                                passingMode = "Normal"
                            },
                            new()
                            {
                                name = "objB",
                                type = Named("System.Object"),
                                passingMode = "Normal"
                            }
                        },
                        abiReturnType = Named("System.Boolean")
                    }
                }
            };
        }

        private static SobakasuCompilationEnvironment CreateCatalogGenericEnvironment()
        {
            return new SobakasuCompilationEnvironment(
                UdonApiCatalogLoader.Load(
                    UdonApiCatalogGenerator.Serialize(
                        CreateCatalogGenericData())));
        }

        private static UdonApiCatalogData CreateCatalogGenericData()
        {
            return new UdonApiCatalogData
            {
                types = new List<UdonApiTypeRecord>
                {
                    new()
                    {
                        runtimeName = "System.Int32",
                        shape = "Value"
                    },
                    new()
                    {
                        runtimeName = "System.Collections.Generic.List`1",
                        shape = "Reference",
                        genericArity = 1
                    },
                    new()
                    {
                        runtimeName = "Test.Component",
                        shape = "Reference"
                    },
                    new()
                    {
                        runtimeName = "Test.Transform",
                        shape = "Reference",
                        supertypes = new List<ExternTypeRef>
                        {
                            Named("Test.Component")
                        }
                    },
                    new()
                    {
                        runtimeName = "Test.Host",
                        shape = "Reference"
                    }
                },
                members = new List<UdonApiMemberRecord>
                {
                    new()
                    {
                        hostType = Named("Test.Host"),
                        clrDeclaringType = Named("Test.Host"),
                        name = "Get",
                        kind = "Method",
                        origin = "Clr",
                        isStatic = true,
                        externSignature = "TestHost.__Get__T__T",
                        genericParameters = new List<UdonApiGenericParameterRecord>
                        {
                            new()
                            {
                                name = "T",
                                typeConstraints = new List<ExternTypeRef>
                                {
                                    Named("Test.Component")
                                }
                            }
                        },
                        abiParameters = new List<ExternParameterRecord>
                        {
                            new()
                            {
                                name = "type",
                                type = GenericParameter(0),
                                passingMode = "GenericTypeArgument"
                            }
                        },
                        abiReturnType = GenericParameter(0)
                    }
                }
            };
        }

        private static ExternTypeRef Named(string runtimeName) => new()
        {
            kind = "Named",
            runtimeName = runtimeName
        };

        private static ExternTypeRef GenericParameter(int ordinal) => new()
        {
            kind = "GenericParameter",
            ordinal = ordinal
        };
    }
}
