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
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Compiler.UasmAssembler;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class ImplExternTestSupport
    {
        internal const string ProjectedTryGetSignature =
            "TestApi.__TryGet__TestOwnerRef__SystemBoolean";
        internal const string ProjectedMixedSignature =
            "TestApi.__Mixed__SystemInt32Ref_TestOwnerRef_SystemStringRef__SystemInt32";
        internal const string ProjectedValiditySignature =
            "VRCSDKBaseUtilities.__IsValid__TestOwner__SystemBoolean";
        internal const string ProjectedConstructorMaybeSignature =
            "TestFoo.__ctor__TestOwnerRef__TestFoo";

        internal static SobakasuCompilationEnvironment CreateExternAbiEnvironment()
        {
            return CreateExternAbiCatalogEnvironment();
        }

        internal static SobakasuCompilationEnvironment CreateGenericExternEnvironment()
        {
            return CreateGenericExternCatalogEnvironment();
        }

        private static SobakasuCompilationEnvironment CreateExternAbiCatalogEnvironment()
        {
            const string fixture =
                "Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture";
            var data = new UdonApiCatalogData
            {
                types = new List<UdonApiTypeRecord>
                {
                    Type("System.Int32", "Value"),
                    Type("System.Boolean", "Value"),
                    Type("System.String", "Reference"),
                    Type("System.Void", "Void"),
                    Type(fixture, "Reference")
                },
                members = new List<UdonApiMemberRecord>
                {
                    Method(fixture, "RefOnly",
                        "Skytomo221SobakasuTestsEditorSobakasuExternAbiFixture.__RefOnly__SystemInt32Ref__SystemVoid",
                        "System.Void", Parameter("value", "System.Int32", "Ref")),
                    Method(fixture, "OutOnly",
                        "Skytomo221SobakasuTestsEditorSobakasuExternAbiFixture.__OutOnly__SystemInt32Ref__SystemVoid",
                        "System.Void", Parameter("value", "System.Int32", "Out")),
                    Method(fixture, "ReturnAndOut",
                        "Skytomo221SobakasuTestsEditorSobakasuExternAbiFixture.__ReturnAndOut__SystemInt32Ref__SystemBoolean",
                        "System.Boolean", Parameter("value", "System.Int32", "Out")),
                    Method(fixture, "Mixed",
                        "Skytomo221SobakasuTestsEditorSobakasuExternAbiFixture.__Mixed__SystemInt32_SystemInt32Ref_SystemStringRef_SystemBooleanRef__SystemInt32",
                        "System.Int32",
                        Parameter("normal", "System.Int32", "Normal"),
                        Parameter("value", "System.Int32", "Ref"),
                        Parameter("text", "System.String", "Out"),
                        Parameter("flag", "System.Boolean", "Ref"))
                }
            };
            return SobakasuCompilationEnvironment.FromUdonApiCatalogJson(
                UdonApiCatalogJson.Serialize(data));
        }

        internal static SobakasuCompilationEnvironment CreateIntegerOperatorEnvironment(
            IReadOnlyList<string> signatures)
        {
            var members = new List<UdonApiMemberRecord>();
            foreach (var signature in signatures)
            {
                var name = signature.Contains("UnaryNegation") ? "op_UnaryNegation" :
                    signature.Contains("OnesComplement") ? "op_OnesComplement" :
                    "op_Addition";
                var parameterCount = name == "op_Addition" ? 2 : 1;
                var parameters = new List<ExternParameterRecord>();
                for (var index = 0; index < parameterCount; index++)
                    parameters.Add(Parameter($"value{index}", "System.Int32", "Normal"));
                members.Add(new UdonApiMemberRecord
                {
                    hostType = Named("System.Int32"),
                    clrDeclaringType = Named("System.Int32"),
                    name = name,
                    kind = "Operator",
                    origin = "Clr",
                    isStatic = true,
                    externSignature = signature,
                    abiParameters = parameters,
                    abiReturnType = Named("System.Int32")
                });
            }

            return SobakasuCompilationEnvironment.FromUdonApiCatalogJson(
                UdonApiCatalogJson.Serialize(new UdonApiCatalogData
                {
                    types = new List<UdonApiTypeRecord> { Type("System.Int32", "Value") },
                    members = members
                }));
        }

        private static SobakasuCompilationEnvironment CreateGenericExternCatalogEnvironment()
        {
            const string fixture =
                "Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericExternFixture";
            var data = new UdonApiCatalogData
            {
                types = new List<UdonApiTypeRecord>
                {
                    Type("System.Void", "Void"),
                    Type("System.String", "Reference"),
                    Type("System.Object", "Reference"),
                    Type("System.Type", "Reference"),
                    Type("System.Collections.Generic.List`1", "Reference", 1),
                    Type(fixture, "Reference"),
                    Type("Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericConstraintBase", "Reference"),
                    Type("Skytomo221.Sobakasu.Tests.Editor.ISobakasuGenericConstraint", "Reference")
                },
                members = new List<UdonApiMemberRecord>
                {
                    GenericMethod(fixture, ".ctor",
                        "Skytomo221SobakasuTestsEditorSobakasuGenericExternFixture.__ctor__Skytomo221SobakasuTestsEditorSobakasuGenericExternFixture",
                        "Constructor", Named(fixture), isStatic: true),
                    GenericMethod(fixture, "Echo",
                        "Skytomo221SobakasuTestsEditorSobakasuGenericExternFixture.__Echo__SystemType_T__T",
                        "Method", Generic(0),
                        new[]
                        {
                            Parameter("type", "System.Type", "GenericTypeArgument"),
                            GenericParameter("value", 0, "Normal")
                        },
                        new[] { GenericConstraint("T", referenceType: true) }),
                    GenericMethod(fixture, "Values",
                        "Skytomo221SobakasuTestsEditorSobakasuGenericExternFixture.__Values__SystemType__TArray",
                        "Method", ApiArray(Generic(0)),
                        new[] { Parameter("type", "System.Type", "GenericTypeArgument") },
                        new[] { GenericConstraint("T") }),
                    GenericMethod(fixture, "Fill",
                        "Skytomo221SobakasuTestsEditorSobakasuGenericExternFixture.__Fill__SystemType_SystemCollectionsGenericListT__SystemVoid",
                        "Method", Named("System.Void"),
                        new[]
                        {
                            Parameter("type", "System.Type", "GenericTypeArgument"),
                            ConstructedGenericParameter("values", "System.Collections.Generic.List`1", 0)
                        },
                        new[] { GenericConstraint("T") }),
                    GenericMethod(fixture, "FillStrings",
                        "Skytomo221SobakasuTestsEditorSobakasuGenericExternFixture.__FillStrings__SystemCollectionsGenericListSystemString__SystemVoid",
                        "Method", Named("System.Void"),
                        new[] { ConstructedParameter("values", "System.Collections.Generic.List`1", "System.String") }),
                    GenericMethod(fixture, "BaseConstraint", "GenericApi.__BaseConstraint__SystemType__T",
                        "Method", Generic(0),
                        new[] { Parameter("type", "System.Type", "GenericTypeArgument") },
                        new[] { GenericConstraint("T", typeConstraint: "Skytomo221.Sobakasu.Tests.Editor.SobakasuGenericConstraintBase") }),
                    GenericMethod(fixture, "InterfaceConstraint", "GenericApi.__InterfaceConstraint__SystemType__T",
                        "Method", Generic(0),
                        new[] { Parameter("type", "System.Type", "GenericTypeArgument") },
                        new[] { GenericConstraint("T", typeConstraint: "Skytomo221.Sobakasu.Tests.Editor.ISobakasuGenericConstraint") }),
                    GenericMethod(fixture, "StructConstraint", "GenericApi.__StructConstraint__SystemType__T",
                        "Method", Generic(0),
                        new[] { Parameter("type", "System.Type", "GenericTypeArgument") },
                        new[] { GenericConstraint("T", valueType: true) }),
                    GenericMethod(fixture, "ConstructorConstraint", "GenericApi.__ConstructorConstraint__SystemType__T",
                        "Method", Generic(0),
                        new[] { Parameter("type", "System.Type", "GenericTypeArgument") },
                        new[] { GenericConstraint("T", constructor: true) })
                }
            };
            return SobakasuCompilationEnvironment.FromUdonApiCatalogJson(
                UdonApiCatalogJson.Serialize(data));
        }

        private static UdonApiTypeRecord Type(
            string runtimeName,
            string shape,
            int genericArity = 0)
        {
            return new UdonApiTypeRecord
            {
                runtimeName = runtimeName,
                shape = shape,
                genericArity = genericArity
            };
        }

        private static UdonApiMemberRecord Method(
            string hostType,
            string name,
            string externSignature,
            string returnType,
            params ExternParameterRecord[] parameters)
        {
            return new UdonApiMemberRecord
            {
                hostType = Named(hostType),
                clrDeclaringType = Named(hostType),
                name = name,
                kind = "Method",
                origin = "Clr",
                isStatic = true,
                externSignature = externSignature,
                abiParameters = new List<ExternParameterRecord>(parameters),
                abiReturnType = Named(returnType)
            };
        }

        private static ExternParameterRecord Parameter(
            string name,
            string type,
            string passingMode)
        {
            return new ExternParameterRecord
            {
                name = name,
                type = Named(type),
                passingMode = passingMode
            };
        }

        private static UdonApiMemberRecord GenericMethod(
            string hostType,
            string name,
            string externSignature,
            string kind,
            ExternTypeRef returnType,
            IReadOnlyList<ExternParameterRecord> parameters = null,
            IReadOnlyList<UdonApiGenericParameterRecord> genericParameters = null,
            bool isStatic = false)
        {
            return new UdonApiMemberRecord
            {
                hostType = Named(hostType),
                clrDeclaringType = Named(hostType),
                name = name,
                kind = kind,
                origin = "Clr",
                isStatic = isStatic,
                externSignature = externSignature,
                abiParameters = parameters == null
                    ? new List<ExternParameterRecord>()
                    : new List<ExternParameterRecord>(parameters),
                genericParameters = genericParameters == null
                    ? new List<UdonApiGenericParameterRecord>()
                    : new List<UdonApiGenericParameterRecord>(genericParameters),
                abiReturnType = returnType
            };
        }

        private static ExternParameterRecord GenericParameter(
            string name,
            int ordinal,
            string passingMode) => new()
        {
            name = name,
            type = Generic(ordinal),
            passingMode = passingMode
        };

        private static ExternParameterRecord ConstructedParameter(
            string name,
            string definition,
            string argument) => new()
        {
            name = name,
            type = ConstructedGeneric(Named(definition), Named(argument)),
            passingMode = "Normal"
        };

        private static ExternParameterRecord ConstructedGenericParameter(
            string name,
            string definition,
            int argumentOrdinal) => new()
        {
            name = name,
            type = ConstructedGeneric(Named(definition), Generic(argumentOrdinal)),
            passingMode = "Normal"
        };

        private static UdonApiGenericParameterRecord GenericConstraint(
            string name,
            bool referenceType = false,
            bool valueType = false,
            bool constructor = false,
            string typeConstraint = null) => new()
        {
            name = name,
            referenceTypeConstraint = referenceType,
            nonNullableValueTypeConstraint = valueType,
            defaultConstructorConstraint = constructor,
            typeConstraints = string.IsNullOrEmpty(typeConstraint)
                ? new List<ExternTypeRef>()
                : new List<ExternTypeRef> { Named(typeConstraint) }
        };

        private static ExternTypeRef Generic(int ordinal) => new()
        {
            kind = "GenericParameter",
            ordinal = ordinal
        };

        private static ExternTypeRef ApiArray(ExternTypeRef element) => new()
        {
            kind = "Array",
            element = element
        };

        private static ExternTypeRef ConstructedGeneric(
            ExternTypeRef definition,
            params ExternTypeRef[] arguments) => new()
        {
            kind = "ConstructedGeneric",
            definition = definition,
            arguments = new List<ExternTypeRef>(arguments)
        };

        private static ExternTypeRef Named(string runtimeName) => new()
        {
            kind = "Named",
            runtimeName = runtimeName
        };

        internal static SobakasuCompilationEnvironment CreateProjectionEnvironment()
        {
            var globalNamespace = new NamespaceSymbol("<global>", string.Empty);
            var testNamespace = globalNamespace.GetOrAddNamespace("Test");
            var vrcNamespace = globalNamespace.GetOrAddNamespace("VRC");
            var sdkBaseNamespace = vrcNamespace.GetOrAddNamespace("SDKBase");

            var ownerType = TypeSymbol.CreateNamed(
                "Owner",
                "Test.Owner",
                isReferenceType: true,
                runtimeTypeIdentity: RuntimeTypeIdentity.Named("Test.Owner"),
                isExternalBinding: true);
            var apiType = TypeSymbol.CreateNamed(
                "Api",
                "Test.Api",
                isReferenceType: true,
                runtimeTypeIdentity: RuntimeTypeIdentity.Named("Test.Api"),
                isExternalBinding: true);
            var fooType = TypeSymbol.CreateNamed(
                "Foo",
                "Test.Foo",
                isReferenceType: true,
                runtimeTypeIdentity: RuntimeTypeIdentity.Named("Test.Foo"),
                isExternalBinding: true);
            var utilitiesType = TypeSymbol.CreateNamed(
                "Utilities",
                "VRC.SDKBase.Utilities",
                isReferenceType: true,
                runtimeTypeIdentity:
                    RuntimeTypeIdentity.Named("VRC.SDKBase.Utilities"),
                isExternalBinding: true);
            testNamespace.AddType(ownerType);
            testNamespace.AddType(apiType);
            testNamespace.AddType(fooType);
            sdkBaseNamespace.AddType(utilitiesType);

            var tryGetMethod = new ExternMethodSymbol(
                "TryGet",
                apiType,
                Array.Empty<ParameterSymbol>(),
                TypeSymbol.Tuple(new[] { TypeSymbol.Bool, ownerType }),
                null,
                ProjectedTryGetSignature,
                isStatic: true,
                memberKind: ExternMemberKind.Method,
                abiParameters: new[]
                {
                    new ExternParameterSymbol(
                        "owner",
                        ownerType,
                        ExternParameterPassingMode.Out,
                        -1)
                },
                abiReturnType: TypeSymbol.Bool);

            var outIntMethod = new ExternMethodSymbol(
                "OutInt",
                apiType,
                Array.Empty<ParameterSymbol>(),
                TypeSymbol.I32,
                null,
                "TestApi.__OutInt__SystemInt32Ref__SystemVoid",
                isStatic: true,
                memberKind: ExternMemberKind.Method,
                abiParameters: new[]
                {
                    new ExternParameterSymbol(
                        "value",
                        TypeSymbol.I32,
                        ExternParameterPassingMode.Out,
                        -1)
                },
                abiReturnType: TypeSymbol.Unit);

            var mixedAbiParameters = new[]
            {
                new ExternParameterSymbol(
                    "value",
                    TypeSymbol.I32,
                    ExternParameterPassingMode.Ref,
                    0),
                new ExternParameterSymbol(
                    "owner",
                    ownerType,
                    ExternParameterPassingMode.Out,
                    -1),
                new ExternParameterSymbol(
                    "text",
                    TypeSymbol.String,
                    ExternParameterPassingMode.Out,
                    -1)
            };
            var mixedMethod = new ExternMethodSymbol(
                "Mixed",
                apiType,
                new[] { new ParameterSymbol("value", TypeSymbol.I32, 0) },
                TypeSymbol.Tuple(new[]
                {
                    TypeSymbol.I32,
                    TypeSymbol.I32,
                    ownerType,
                    TypeSymbol.String
                }),
                null,
                ProjectedMixedSignature,
                isStatic: true,
                memberKind: ExternMemberKind.Method,
                abiParameters: mixedAbiParameters,
                abiReturnType: TypeSymbol.I32);

            var valueConstructor = CreateFakeConstructor(
                fooType,
                "TestFoo.__ctor__SystemInt32__TestFoo",
                new[] { new ParameterSymbol("value", TypeSymbol.I32, 0) },
                new[]
                {
                    new ExternParameterSymbol(
                        "value",
                        TypeSymbol.I32,
                        ExternParameterPassingMode.Normal,
                        0)
                },
                fooType);
            var refValueConstructor = CreateFakeConstructor(
                fooType,
                "TestFoo.__ctor__SystemInt32Ref__TestFoo",
                new[] { new ParameterSymbol("value", TypeSymbol.I32, 0) },
                new[]
                {
                    new ExternParameterSymbol(
                        "value",
                        TypeSymbol.I32,
                        ExternParameterPassingMode.Ref,
                        0)
                },
                TypeSymbol.Tuple(new[] { fooType, TypeSymbol.I32 }));
            var outNameConstructor = CreateFakeConstructor(
                fooType,
                "TestFoo.__ctor__SystemStringRef__TestFoo",
                Array.Empty<ParameterSymbol>(),
                new[]
                {
                    new ExternParameterSymbol(
                        "name",
                        TypeSymbol.String,
                        ExternParameterPassingMode.Out,
                        -1)
                },
                TypeSymbol.Tuple(new[] { fooType, TypeSymbol.String }));
            var mixedConstructor = CreateFakeConstructor(
                fooType,
                "TestFoo.__ctor__SystemInt32Ref_SystemStringRef_SystemSingleRef__TestFoo",
                new[]
                {
                    new ParameterSymbol("value", TypeSymbol.I32, 0),
                    new ParameterSymbol("weight", TypeSymbol.F32, 1)
                },
                new[]
                {
                    new ExternParameterSymbol(
                        "value",
                        TypeSymbol.I32,
                        ExternParameterPassingMode.Ref,
                        0),
                    new ExternParameterSymbol(
                        "name",
                        TypeSymbol.String,
                        ExternParameterPassingMode.Out,
                        -1),
                    new ExternParameterSymbol(
                        "weight",
                        TypeSymbol.F32,
                        ExternParameterPassingMode.Ref,
                        1)
                },
                TypeSymbol.Tuple(new[]
                {
                    fooType,
                    TypeSymbol.I32,
                    TypeSymbol.String,
                    TypeSymbol.F32
                }));
            var maybeConstructor = CreateFakeConstructor(
                fooType,
                ProjectedConstructorMaybeSignature,
                Array.Empty<ParameterSymbol>(),
                new[]
                {
                    new ExternParameterSymbol(
                        "owner",
                        ownerType,
                        ExternParameterPassingMode.Out,
                        -1)
                },
                TypeSymbol.Tuple(new[] { fooType, ownerType }));

            var isValidMethod = new ExternMethodSymbol(
                "IsValid",
                utilitiesType,
                new[] { new ParameterSymbol("value", ownerType, 0) },
                TypeSymbol.Bool,
                null,
                ProjectedValiditySignature,
                isStatic: true);

            var typesByName = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
            {
                [ownerType.QualifiedName] = ownerType,
                [apiType.QualifiedName] = apiType,
                [fooType.QualifiedName] = fooType,
                [utilitiesType.QualifiedName] = utilitiesType
            };
            var memberGroups = CreateMemberGroups(
                tryGetMethod,
                outIntMethod,
                mixedMethod,
                valueConstructor,
                refValueConstructor,
                outNameConstructor,
                mixedConstructor,
                maybeConstructor,
                isValidMethod);
            var metadata = new Dictionary<TypeSymbol, ExternTypeMetadata>
            {
                [TypeSymbol.Unit] = CreateAbiMetadata(
                    TypeSymbol.Unit, ExternTypeShape.Void),
                [TypeSymbol.Bool] = CreateAbiMetadata(
                    TypeSymbol.Bool, ExternTypeShape.Value),
                [TypeSymbol.I32] = CreateAbiMetadata(
                    TypeSymbol.I32, ExternTypeShape.Value),
                [TypeSymbol.F32] = CreateAbiMetadata(
                    TypeSymbol.F32, ExternTypeShape.Value),
                [TypeSymbol.String] = CreateAbiMetadata(
                    TypeSymbol.String, ExternTypeShape.Reference),
                [ownerType] = CreateAbiMetadata(
                    ownerType, ExternTypeShape.Reference),
                [apiType] = CreateAbiMetadata(
                    apiType, ExternTypeShape.Reference),
                [fooType] = CreateAbiMetadata(
                    fooType, ExternTypeShape.Reference),
                [utilitiesType] = CreateAbiMetadata(
                    utilitiesType, ExternTypeShape.Reference)
            };
            var catalog = new ExternCatalog(
                globalNamespace,
                typesByName,
                metadata,
                memberGroups,
                new Dictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>(),
                new Dictionary<RuntimeTypeIdentity, ArrayIntrinsicSymbols>());
            return new SobakasuCompilationEnvironment(catalog);
        }
        private static ExternMethodSymbol CreateFakeConstructor(
            TypeSymbol containingType,
            string signature,
            IReadOnlyList<ParameterSymbol> parameters,
            IReadOnlyList<ExternParameterSymbol> abiParameters,
            TypeSymbol logicalReturnType)
        {
            return new ExternMethodSymbol(
                "new",
                containingType,
                parameters,
                logicalReturnType,
                null,
                signature,
                isStatic: true,
                memberKind: ExternMemberKind.Constructor,
                abiParameters: abiParameters,
                abiReturnType: containingType);
        }
        internal static (
            BoundProgram Program,
            IrProgram Ir,
            string Uasm) CompileWithEnvironment(
                string source,
                SobakasuCompilationEnvironment environment)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));

            var binder = new SobakasuBinder(environment);
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty,
                Format(binder.Diagnostics.Diagnostics));

            var desugarer = new SobakasuDesugarer();
            var desugared = desugarer.Desugar(program);
            Assert.That(desugarer.Diagnostics.Diagnostics, Is.Empty,
                Format(desugarer.Diagnostics.Diagnostics));

            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(desugared);
            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty,
                Format(lowerer.Diagnostics.Diagnostics));

            var optimized = new SobakasuOptimizer().Optimize(ir);
            var assembler = new SobakasuUasmAssembler();
            var uasm = assembler.Assemble(optimized);
            Assert.That(assembler.Diagnostics.Diagnostics, Is.Empty,
                Format(assembler.Diagnostics.Diagnostics));
            return (program, ir, uasm);
        }
        internal static ExternMethodSymbol FindExternalMethod(
            BoundProgram program,
            string functionName)
        {
            return program.Functions.Single(function =>
                    function.FunctionSymbol.Name == functionName)
                .FunctionSymbol.ExternalBinding.ExternalMethod;
        }
        internal static IrExternCallInstruction FindExternCall(
            IrProgram program,
            string signature)
        {
            foreach (var module in program.Modules)
                foreach (var block in module.Blocks)
                    foreach (var instruction in block.Instructions)
                    {
                        if (instruction is IrExternCallInstruction call &&
                            call.ExternSignature == signature)
                        {
                            return call;
                        }
                    }

            Assert.Fail($"Extern call '{signature}' was not lowered.");
            return null;
        }
        internal static int CountExternCalls(IrProgram program, string signature)
        {
            var count = 0;
            foreach (var module in program.Modules)
                foreach (var block in module.Blocks)
                    foreach (var instruction in block.Instructions)
                    {
                        if (instruction is IrExternCallInstruction call &&
                            call.ExternSignature == signature)
                        {
                            count++;
                        }
                    }

            return count;
        }
        internal static bool HasCopyBeforeCall(
            IrProgram program,
            IrExternCallInstruction expectedCall,
            IrValue value)
        {
            foreach (var module in program.Modules)
                foreach (var block in module.Blocks)
                {
                    for (var index = 0; index < block.Instructions.Count; index++)
                    {
                        if (!ReferenceEquals(block.Instructions[index], expectedCall))
                            continue;

                        for (var copyIndex = 0; copyIndex < index; copyIndex++)
                        {
                            if (block.Instructions[copyIndex] is IrCopyInstruction copy &&
                                ReferenceEquals(copy.Target, value))
                            {
                                return true;
                            }
                        }
                        return false;
                    }
                }

            Assert.Fail("The expected extern call was not found in the IR.");
            return false;
        }
        internal static SobakasuBinder Bind(
            string source,
            SobakasuCompilationEnvironment environment = null)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty,
                Format(parser.Diagnostics.Diagnostics));
            var binder = environment == null
                ? new SobakasuBinder(SobakasuTestEnvironment.Default)
                : new SobakasuBinder(environment);
            binder.BindProgram(syntax);
            return binder;
        }
        internal static SobakasuCompilationEnvironment CreateAmbiguousExternEnvironment()
        {
            var globalNamespace = new NamespaceSymbol("<global>", string.Empty);
            var testNamespace = globalNamespace.GetOrAddNamespace("Test");
            var apiType = TypeSymbol.CreateNamed("Api", "Test.Api");
            var parameters = new[]
            {
                new ParameterSymbol("value", TypeSymbol.I32, 0)
            };
            var firstMethod = new ExternMethodSymbol(
                "Call",
                apiType,
                parameters,
                TypeSymbol.Unit,
                typeof(ImplExternTestSupport).GetMethod(
                    nameof(AmbiguousExternCandidateA),
                    BindingFlags.Static | BindingFlags.NonPublic),
                "TestApi.__CallA__SystemInt32__SystemVoid");
            var secondMethod = new ExternMethodSymbol(
                "Call",
                apiType,
                parameters,
                TypeSymbol.Unit,
                typeof(ImplExternTestSupport).GetMethod(
                    nameof(AmbiguousExternCandidateB),
                    BindingFlags.Static | BindingFlags.NonPublic),
                "TestApi.__CallB__SystemInt32__SystemVoid");
            testNamespace.AddType(apiType);

            var typesByName = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
            {
                [TypeSymbol.Unit.RuntimeQualifiedName] = TypeSymbol.Unit,
                [TypeSymbol.I32.RuntimeQualifiedName] = TypeSymbol.I32,
                [apiType.QualifiedName] = apiType
            };
            var catalog = new ExternCatalog(
                globalNamespace,
                typesByName,
                new Dictionary<TypeSymbol, ExternTypeMetadata>(),
                CreateMemberGroups(firstMethod, secondMethod),
                new Dictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>(),
                new Dictionary<RuntimeTypeIdentity, ArrayIntrinsicSymbols>());
            return new SobakasuCompilationEnvironment(catalog);
        }

        private static ExternTypeMetadata CreateAbiMetadata(
            TypeSymbol type,
            ExternTypeShape shape)
        {
            var identity = type.RuntimeTypeIdentity ??
                RuntimeTypeIdentity.Named(type.RuntimeQualifiedName);
            return new ExternTypeMetadata(
                identity,
                shape,
                type.GenericParameters.Count,
                Array.Empty<RuntimeTypeIdentity>(),
                isAbiAvailable: true,
                satisfiesDefaultConstructorConstraint: false,
                @enum: null);
        }

        private static IReadOnlyDictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>> CreateMemberGroups(params ExternMethodSymbol[] methods)
        {
            var groupsByType = new Dictionary<TypeSymbol, Dictionary<string, MethodGroupSymbol>>();
            foreach (var method in methods)
            {
                if (!groupsByType.TryGetValue(method.ContainingType, out var groups))
                {
                    groups = new Dictionary<string, MethodGroupSymbol>(StringComparer.Ordinal);
                    groupsByType.Add(method.ContainingType, groups);
                }
                if (!groups.TryGetValue(method.Name, out var group))
                {
                    group = new MethodGroupSymbol(method.Name, method.ContainingType);
                    groups.Add(method.Name, group);
                }
                group.AddMethod(method);
            }

            var result = new Dictionary<TypeSymbol, IReadOnlyDictionary<string, MethodGroupSymbol>>();
            foreach (var pair in groupsByType)
                result.Add(pair.Key, new Dictionary<string, MethodGroupSymbol>(pair.Value, StringComparer.Ordinal));
            return result;
        }
        internal static void AmbiguousExternCandidateA(int value)
        {
        }
        internal static void AmbiguousExternCandidateB(int value)
        {
        }
        internal sealed class ProjectionOwnerFixture
        {
        }
        internal sealed class ProjectionFooFixture
        {
        }
        internal static List<SyntaxToken> LexAll(string source)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));
            var tokens = new List<SyntaxToken>();
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                tokens.Add(token);
            }
            while (token.Kind != SyntaxKind.EndOfFile);

            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty,
                Format(lexer.Diagnostics.Diagnostics));
            return tokens;
        }
        internal static bool ContainsCode(
            IReadOnlyList<Diagnostic> diagnostics,
            string code)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }
        internal static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
        internal static string Format(IReadOnlyList<Diagnostic> diagnostics)
        {
            var lines = new List<string>();
            foreach (var diagnostic in diagnostics)
                lines.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", lines);
        }
    }
}
