using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuUseDirectiveTests
    {
        private const string DebugLogExternSignature =
            "UnityEngineDebug.__Log__SystemObject__SystemVoid";

        [Test]
        public void Parser_ParsesNamespaceUseDirective()
        {
            var syntax = ParseCompilationUnit("use UnityEngine;");

            var useDirective = syntax.Members[0] as UseDirectiveSyntax;
            Assert.That(useDirective, Is.Not.Null);
            Assert.That(useDirective.Path.GetText(), Is.EqualTo("UnityEngine"));
            Assert.That(useDirective.Alias, Is.Null);
        }

        [Test]
        public void Parser_ParsesTypeUseDirective()
        {
            var syntax = ParseCompilationUnit("use UnityEngine.Debug;");

            var useDirective = syntax.Members[0] as UseDirectiveSyntax;
            Assert.That(useDirective, Is.Not.Null);
            Assert.That(useDirective.Path.GetText(), Is.EqualTo("UnityEngine.Debug"));
            Assert.That(useDirective.Alias, Is.Null);
        }

        [Test]
        public void Parser_ParsesFunctionAliasUseDirective()
        {
            var syntax = ParseCompilationUnit("use UnityEngine.Debug.Log as log;");

            var useDirective = syntax.Members[0] as UseDirectiveSyntax;
            Assert.That(useDirective, Is.Not.Null);
            Assert.That(useDirective.Path.GetText(), Is.EqualTo("UnityEngine.Debug.Log"));
            Assert.That(useDirective.Alias.Text, Is.EqualTo("log"));
        }

        [Test]
        public void Parser_ParsesTypeAliasUseDirective()
        {
            var syntax = ParseCompilationUnit("use UnityEngine.Debug as D;");

            var useDirective = syntax.Members[0] as UseDirectiveSyntax;
            Assert.That(useDirective, Is.Not.Null);
            Assert.That(useDirective.Path.GetText(), Is.EqualTo("UnityEngine.Debug"));
            Assert.That(useDirective.Alias.Text, Is.EqualTo("D"));
        }

        [Test]
        public void Parser_ReportsInvalidUseDirectiveForMalformedAlias()
        {
            var parser = new SobakasuParser(SourceText.From("use UnityEngine.Debug as ;"));
            parser.ParseCompilationUnit();

            Assert.That(ContainsDiagnosticCode(parser.Diagnostics.Diagnostics, "SBK1004"), Is.True);
        }

        [Test]
        public void Binder_ResolvesDebugLogThroughNamespaceImport()
        {
            var program = BindProgram(
                @"use UnityEngine;
on Interact() {
  Debug.Log(""Hello"");
}",
                CreateSyntheticEnvironment());

            var callExpression = GetSingleCallExpression(program);
            Assert.That(callExpression.Method, Is.Not.Null);
            Assert.That(callExpression.Method.ExternSignature, Is.EqualTo(DebugLogExternSignature));
        }

        [Test]
        public void Binder_ResolvesLogThroughDirectFunctionImport()
        {
            var program = BindProgram(
                @"use UnityEngine.Debug.Log;
on Interact() {
  Log(""Hello"");
}",
                CreateSyntheticEnvironment());

            var callExpression = GetSingleCallExpression(program);
            Assert.That(callExpression.Method, Is.Not.Null);
            Assert.That(callExpression.Method.ExternSignature, Is.EqualTo(DebugLogExternSignature));
        }

        [Test]
        public void Binder_ResolvesLogThroughAliasImport()
        {
            var program = BindProgram(
                @"use UnityEngine.Debug.Log as log;
on Interact() {
  log(""Hello"");
}",
                CreateSyntheticEnvironment());

            var callExpression = GetSingleCallExpression(program);
            Assert.That(callExpression.Method, Is.Not.Null);
            Assert.That(callExpression.Method.ExternSignature, Is.EqualTo(DebugLogExternSignature));
        }

        [Test]
        public void Binder_ResolvesTypeAliasImport()
        {
            var program = BindProgram(
                @"use UnityEngine.Debug as D;
on Interact() {
  D.Log(""Hello"");
}",
                CreateSyntheticEnvironment());

            var callExpression = GetSingleCallExpression(program);
            Assert.That(callExpression.Method, Is.Not.Null);
            Assert.That(callExpression.Method.ExternSignature, Is.EqualTo(DebugLogExternSignature));
        }

        [Test]
        public void Binder_ReportsUndefinedNameWithoutImport()
        {
            var binder = CreateBinder(
                @"on Interact() {
  Log(""Hello"");
}",
                CreateSyntheticEnvironment());

            Assert.That(ContainsDiagnosticCode(binder.Diagnostics.Diagnostics, "SBK2002"), Is.True);
        }

        [Test]
        public void Binder_ReportsAmbiguousReferenceForConflictingNamespaceImports()
        {
            var binder = CreateBinder(
                @"use UnityEngine;
use CustomEngine;
on Interact() {
  Debug.Log(""Hello"");
}",
                CreateSyntheticEnvironment(includeConflictingNamespaceDebug: true));

            Assert.That(ContainsDiagnosticCode(binder.Diagnostics.Diagnostics, "SBK2021"), Is.True);
        }

        [Test]
        public void Binder_ReportsAmbiguousExternOverload()
        {
            var binder = CreateBinder(
                @"use UnityEngine.Debug.Log;
on Interact() {
  Log(""Hello"");
}",
                CreateSyntheticEnvironment(includeAmbiguousLogOverloads: true));

            Assert.That(ContainsDiagnosticCode(binder.Diagnostics.Diagnostics, "SBK2023"), Is.True);
        }

        [Test]
        public void Binder_ReportsRejectedOnlyExternMethodGroup()
        {
            var binder = CreateBinder(
                @"use UnityEngine.Debug.Log;
on Interact() {
  Log(""Hello"");
}",
                CreateSyntheticEnvironment(includeRejectedOnlyLog: true));

            Assert.That(ContainsDiagnosticCode(binder.Diagnostics.Diagnostics, "SBK2024"), Is.True);
        }

        [Test]
        public void CompileToUasm_ResolvesNamespaceImportAndEmitsExtern()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use UnityEngine;
on Interact() {
  Debug.Log(""Hello"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(DebugLogExternSignature));
        }

        [Test]
        public void CompileToUasm_ResolvesDirectFunctionImportAndEmitsExtern()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use UnityEngine.Debug.Log;
on Interact() {
  Log(""Hello"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(DebugLogExternSignature));
        }

        [Test]
        public void CompileToUasm_ResolvesAliasImportAndEmitsExtern()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use UnityEngine.Debug.Log as log;
on Interact() {
  log(""Hello"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(DebugLogExternSignature));
        }

        [Test]
        public void CompileToUasm_ResolvesTypeAliasImportAndEmitsExtern()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"use UnityEngine.Debug as D;
on Interact() {
  D.Log(""Hello"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(DebugLogExternSignature));
        }

        [Test]
        public void CompileToUasm_Regression_BareDebugLogStillWorks()
        {
            var result = SobakasuCompiler.CompileToUasm(
                @"on Interact() {
  Debug.Log(""Hello"");
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain(DebugLogExternSignature));
        }

        private static CompilationUnitSyntax ParseCompilationUnit(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);
            return syntax;
        }

        private static SobakasuBinder CreateBinder(
            string source,
            SobakasuCompilationEnvironment environment)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);

            var binder = new SobakasuBinder(environment);
            binder.BindProgram(syntax);
            return binder;
        }

        private static BoundProgram BindProgram(
            string source,
            SobakasuCompilationEnvironment environment)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);

            var binder = new SobakasuBinder(environment);
            var program = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty, BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));
            return program;
        }

        private static BoundCallExpression GetSingleCallExpression(BoundProgram program)
        {
            var statement = program.Events[0].Body.Statements[0] as BoundExpressionStatement;
            Assert.That(statement, Is.Not.Null);

            var callExpression = statement.Expression as BoundCallExpression;
            Assert.That(callExpression, Is.Not.Null);
            return callExpression;
        }

        private static SobakasuCompilationEnvironment CreateSyntheticEnvironment(
            bool includeConflictingNamespaceDebug = false,
            bool includeRejectedOnlyLog = false,
            bool includeAmbiguousLogOverloads = false)
        {
            var globalNamespace = new NamespaceSymbol("<global>", "");
            var typeSymbolsByClrType = new Dictionary<Type, TypeSymbol>
            {
                [typeof(object)] = TypeSymbol.Object
            };
            var typeSymbolsByQualifiedName = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
            {
                [TypeSymbol.Object.QualifiedName] = TypeSymbol.Object
            };

            var unityEngineNamespace = globalNamespace.GetOrAddNamespace("UnityEngine");
            var debugType = TypeSymbol.CreateNamed("Debug", "UnityEngine.Debug");
            unityEngineNamespace.AddType(debugType);
            typeSymbolsByQualifiedName[debugType.QualifiedName] = debugType;

            if (includeRejectedOnlyLog)
            {
                debugType.AddRejectedCandidate(
                    "Log",
                    new ExternCandidate(
                        GetHelperMethod(nameof(RejectedLog)),
                        DebugLogExternSignature,
                        false,
                        "The computed extern signature is not exposed to Udon."));
            }
            else
            {
                debugType.AddMethod(CreateExternMethod(
                    debugType,
                    "Log",
                    GetHelperMethod(nameof(StubLogObject)),
                    new[] { TypeSymbol.Object },
                    TypeSymbol.Void,
                    DebugLogExternSignature));
            }

            if (includeAmbiguousLogOverloads)
            {
                debugType.AddMethod(CreateExternMethod(
                    debugType,
                    "Log",
                    GetHelperMethod(nameof(StubLogObjectAlt)),
                    new[] { TypeSymbol.Object },
                    TypeSymbol.Void,
                    DebugLogExternSignature + "__alt"));
            }

            if (includeConflictingNamespaceDebug)
            {
                var customNamespace = globalNamespace.GetOrAddNamespace("CustomEngine");
                var customDebugType = TypeSymbol.CreateNamed("Debug", "CustomEngine.Debug");
                customNamespace.AddType(customDebugType);
                typeSymbolsByQualifiedName[customDebugType.QualifiedName] = customDebugType;
                customDebugType.AddMethod(CreateExternMethod(
                    customDebugType,
                    "Log",
                    GetHelperMethod(nameof(StubLogObject)),
                    new[] { TypeSymbol.Object },
                    TypeSymbol.Void,
                    "CustomEngineDebug.__Log__SystemObject__SystemVoid"));
            }

            var catalog = new ExternCatalog(
                globalNamespace,
                typeSymbolsByClrType,
                typeSymbolsByQualifiedName);
            var compatibilitySymbols = new Dictionary<string, Symbol>(StringComparer.Ordinal);
            return new SobakasuCompilationEnvironment(catalog, compatibilitySymbols);
        }

        private static ExternMethodSymbol CreateExternMethod(
            TypeSymbol containingType,
            string name,
            MethodInfo methodInfo,
            IReadOnlyList<TypeSymbol> parameterTypes,
            TypeSymbol returnType,
            string externSignature)
        {
            var parameters = new List<ParameterSymbol>();
            for (var index = 0; index < parameterTypes.Count; index++)
            {
                parameters.Add(new ParameterSymbol($"arg{index}", parameterTypes[index], index));
            }

            return new ExternMethodSymbol(
                name,
                containingType,
                parameters,
                returnType,
                methodInfo,
                externSignature);
        }

        private static MethodInfo GetHelperMethod(string name)
        {
            return typeof(SobakasuUseDirectiveTests).GetMethod(
                name,
                BindingFlags.Static | BindingFlags.NonPublic);
        }

        private static string BuildDiagnosticMessage(
            IReadOnlyList<Diagnostic> diagnostics)
        {
            if (diagnostics.Count == 0)
            {
                return string.Empty;
            }

            var lines = new string[diagnostics.Count];
            for (var index = 0; index < diagnostics.Count; index++)
            {
                lines[index] = $"{diagnostics[index].Code}: {diagnostics[index].Message}";
            }

            return string.Join("\n", lines);
        }

        private static bool ContainsDiagnosticCode(
            IReadOnlyList<Diagnostic> diagnostics,
            string expectedCode)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == expectedCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static void StubLogObject(object value)
        {
        }

        private static void StubLogObjectAlt(object value)
        {
        }

        private static void RejectedLog(object value)
        {
        }
    }
}
