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
            var signatures = typeof(SobakasuExternAbiFixture)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.DeclaringType == typeof(SobakasuExternAbiFixture))
                .Select(UdonExternSignatureFormatter.GetUdonMethodName)
                .ToArray();
            var catalog = new ReflectionExternCatalogBuilder(
                new UdonExposedNodeCache(signatures))
                .BuildCatalog(new[] { typeof(SobakasuExternAbiFixture).Namespace });
            return new SobakasuCompilationEnvironment(catalog);
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
            var catalog = new ReflectionExternCatalogBuilder(
                new UdonExposedNodeCache(signatures))
                .BuildCatalog(new[]
                {
                    type.Namespace,
                    typeof(List<>).Namespace
                });
            return new SobakasuCompilationEnvironment(catalog);
        }
        internal static SobakasuCompilationEnvironment CreateProjectionEnvironment()
        {
            var globalNamespace = new NamespaceSymbol("<global>", string.Empty);
            var testNamespace = globalNamespace.GetOrAddNamespace("Test");
            var vrcNamespace = globalNamespace.GetOrAddNamespace("VRC");
            var sdkBaseNamespace = vrcNamespace.GetOrAddNamespace("SDKBase");

            var ownerType = TypeSymbol.CreateNamed("Owner", "Test.Owner");
            var apiType = TypeSymbol.CreateNamed("Api", "Test.Api");
            var fooType = TypeSymbol.CreateNamed("Foo", "Test.Foo");
            var utilitiesType = TypeSymbol.CreateNamed(
                "Utilities",
                "VRC.SDKBase.Utilities");
            testNamespace.AddType(ownerType);
            testNamespace.AddType(apiType);
            testNamespace.AddType(fooType);
            sdkBaseNamespace.AddType(utilitiesType);

            apiType.AddMethod(new ExternMethodSymbol(
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
                abiReturnType: TypeSymbol.Bool));

            apiType.AddMethod(new ExternMethodSymbol(
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
                abiReturnType: TypeSymbol.Unit));

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
            apiType.AddMethod(new ExternMethodSymbol(
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
                abiReturnType: TypeSymbol.I32));

            AddFakeConstructor(
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
            AddFakeConstructor(
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
            AddFakeConstructor(
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
            AddFakeConstructor(
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
            AddFakeConstructor(
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

            utilitiesType.AddMethod(new ExternMethodSymbol(
                "IsValid",
                utilitiesType,
                new[] { new ParameterSymbol("value", ownerType, 0) },
                TypeSymbol.Bool,
                null,
                ProjectedValiditySignature,
                isStatic: true));

            var typesByName = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
            {
                [ownerType.QualifiedName] = ownerType,
                [apiType.QualifiedName] = apiType,
                [fooType.QualifiedName] = fooType,
                [utilitiesType.QualifiedName] = utilitiesType
            };
            var catalog = new ExternCatalog(
                globalNamespace,
                new Dictionary<Type, TypeSymbol>
                {
                    [typeof(void)] = TypeSymbol.Unit,
                    [typeof(bool)] = TypeSymbol.Bool,
                    [typeof(int)] = TypeSymbol.I32,
                    [typeof(float)] = TypeSymbol.F32,
                    [typeof(string)] = TypeSymbol.String,
                    [typeof(ProjectionOwnerFixture)] = ownerType,
                    [typeof(ProjectionFooFixture)] = fooType
                },
                typesByName);
            return new SobakasuCompilationEnvironment(catalog);
        }
        internal static void AddFakeConstructor(
            TypeSymbol containingType,
            string signature,
            IReadOnlyList<ParameterSymbol> parameters,
            IReadOnlyList<ExternParameterSymbol> abiParameters,
            TypeSymbol logicalReturnType)
        {
            containingType.AddMethod(new ExternMethodSymbol(
                "new",
                containingType,
                parameters,
                logicalReturnType,
                null,
                signature,
                isStatic: true,
                memberKind: ExternMemberKind.Constructor,
                abiParameters: abiParameters,
                abiReturnType: containingType));
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
                ? new SobakasuBinder()
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
            apiType.AddMethod(new ExternMethodSymbol(
                "Call",
                apiType,
                parameters,
                TypeSymbol.Unit,
                typeof(ImplExternTestSupport).GetMethod(
                    nameof(AmbiguousExternCandidateA),
                    BindingFlags.Static | BindingFlags.NonPublic),
                "TestApi.__CallA__SystemInt32__SystemVoid"));
            apiType.AddMethod(new ExternMethodSymbol(
                "Call",
                apiType,
                parameters,
                TypeSymbol.Unit,
                typeof(ImplExternTestSupport).GetMethod(
                    nameof(AmbiguousExternCandidateB),
                    BindingFlags.Static | BindingFlags.NonPublic),
                "TestApi.__CallB__SystemInt32__SystemVoid"));
            testNamespace.AddType(apiType);

            var clrTypes = new Dictionary<Type, TypeSymbol>
            {
                [typeof(void)] = TypeSymbol.Unit,
                [typeof(int)] = TypeSymbol.I32
            };
            var typesByName = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
            {
                [TypeSymbol.Unit.RuntimeQualifiedName] = TypeSymbol.Unit,
                [TypeSymbol.I32.RuntimeQualifiedName] = TypeSymbol.I32,
                [apiType.QualifiedName] = apiType
            };
            var catalog = new ExternCatalog(
                globalNamespace,
                clrTypes,
                typesByName);
            return new SobakasuCompilationEnvironment(catalog);
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
