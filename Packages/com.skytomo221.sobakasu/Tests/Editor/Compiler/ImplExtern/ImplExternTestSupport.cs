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
using Skytomo221.Sobakasu.Tools.UdonApi;
using Skytomo221.Sobakasu.Tools.UdonApiCatalog;
using UnityEditor;
using UnityEngine;

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
            var type = typeof(SobakasuExternAbiFixture);
            var signatures = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.DeclaringType == type)
                .Select(UdonExternSignatureFormatter.GetUdonMethodName)
                .ToArray();
            return CreateCatalogEnvironment(new[] { type }, signatures);
        }

        internal static SobakasuCompilationEnvironment CreateGenericExternEnvironment()
        {
            var type = typeof(SobakasuGenericExternFixture);
            var signatures = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.DeclaringType == type)
                .Select(UdonExternSignatureFormatter.GetUdonMethodName)
                .Concat(type.GetConstructors().Select(
                    UdonExternSignatureFormatter.GetUdonMethodName))
                .ToArray();
            return CreateCatalogEnvironment(new[] { type }, signatures);
        }

        internal static SobakasuCompilationEnvironment CreateCatalogEnvironment(
            IReadOnlyList<Type> rootTypes,
            IEnumerable<string> signatures)
        {
            var exposure = new FixtureUdonApiExposure(rootTypes, signatures);
            var generated = new UdonApiCatalogGenerator().Generate(
                rootTypes,
                exposure);
            return new SobakasuCompilationEnvironment(
                UdonApiCatalogLoader.Load(generated.Json));
        }
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

        private sealed class FixtureUdonApiExposure : IUdonApiExposure
        {
            private readonly HashSet<Type> _types;
            private readonly HashSet<string> _signatures;

            internal FixtureUdonApiExposure(
                IEnumerable<Type> types,
                IEnumerable<string> signatures)
            {
                _types = new HashSet<Type>(types ?? Array.Empty<Type>());
                _signatures = new HashSet<string>(
                    signatures ?? Array.Empty<string>(),
                    StringComparer.Ordinal);
            }

            public IReadOnlyCollection<string> ExposedSignatures => _signatures;

            public bool IsTypeExposed(Type type) =>
                type != null && _types.Contains(type);

            public bool IsMemberExposed(string externSignature) =>
                !string.IsNullOrWhiteSpace(externSignature) &&
                _signatures.Contains(externSignature);
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
