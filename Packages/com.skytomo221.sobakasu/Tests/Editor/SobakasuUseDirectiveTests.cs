using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Skytomo221.Sobakasu.Compiler;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.IrLowerer;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Syntax;
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

        [Test]
        public void Parser_RespectsMultiplicativePrecedence()
        {
            var expression = ParseSingleExpression("1 + 2 * 3");

            var addition = expression as BinaryExpressionSyntax;
            Assert.That(addition, Is.Not.Null);
            Assert.That(addition.OperatorToken.Kind, Is.EqualTo(SyntaxKind.PlusToken));
            Assert.That(addition.Left, Is.TypeOf<IntegerLiteralExpressionSyntax>());

            var multiplication = addition.Right as BinaryExpressionSyntax;
            Assert.That(multiplication, Is.Not.Null);
            Assert.That(multiplication.OperatorToken.Kind, Is.EqualTo(SyntaxKind.StarToken));
        }

        [Test]
        public void Parser_RespectsParenthesizedPrecedence()
        {
            var expression = ParseSingleExpression("(1 + 2) * 3");

            var multiplication = expression as BinaryExpressionSyntax;
            Assert.That(multiplication, Is.Not.Null);
            Assert.That(multiplication.OperatorToken.Kind, Is.EqualTo(SyntaxKind.StarToken));

            var parenthesized = multiplication.Left as ParenthesizedExpressionSyntax;
            Assert.That(parenthesized, Is.Not.Null);

            var addition = parenthesized.Expression as BinaryExpressionSyntax;
            Assert.That(addition, Is.Not.Null);
            Assert.That(addition.OperatorToken.Kind, Is.EqualTo(SyntaxKind.PlusToken));
        }

        [Test]
        public void Parser_ParsesAdditiveOperatorsAsLeftAssociative()
        {
            var expression = ParseSingleExpression("a + b + c");

            var outerAddition = expression as BinaryExpressionSyntax;
            Assert.That(outerAddition, Is.Not.Null);
            Assert.That(outerAddition.OperatorToken.Kind, Is.EqualTo(SyntaxKind.PlusToken));
            Assert.That(outerAddition.Right, Is.TypeOf<NameExpressionSyntax>());

            var innerAddition = outerAddition.Left as BinaryExpressionSyntax;
            Assert.That(innerAddition, Is.Not.Null);
            Assert.That(innerAddition.OperatorToken.Kind, Is.EqualTo(SyntaxKind.PlusToken));
        }

        [Test]
        public void Parser_ParsesShiftAfterAdditivePrecedence()
        {
            var expression = ParseSingleExpression("a + b << c");

            var shift = expression as BinaryExpressionSyntax;
            Assert.That(shift, Is.Not.Null);
            Assert.That(shift.OperatorToken.Kind, Is.EqualTo(SyntaxKind.LessLessToken));
            Assert.That(shift.Right, Is.TypeOf<NameExpressionSyntax>());

            var addition = shift.Left as BinaryExpressionSyntax;
            Assert.That(addition, Is.Not.Null);
            Assert.That(addition.OperatorToken.Kind, Is.EqualTo(SyntaxKind.PlusToken));
        }

        [Test]
        public void Parser_ParsesLogicalAndBeforeLogicalOr()
        {
            var expression = ParseSingleExpression("a && b || c");

            var logicalOr = expression as BinaryExpressionSyntax;
            Assert.That(logicalOr, Is.Not.Null);
            Assert.That(logicalOr.OperatorToken.Kind, Is.EqualTo(SyntaxKind.PipePipeToken));
            Assert.That(logicalOr.Right, Is.TypeOf<NameExpressionSyntax>());

            var logicalAnd = logicalOr.Left as BinaryExpressionSyntax;
            Assert.That(logicalAnd, Is.Not.Null);
            Assert.That(logicalAnd.OperatorToken.Kind, Is.EqualTo(SyntaxKind.AmpersandAmpersandToken));
        }

        [Test]
        public void Parser_KeepsPostfixPrecedenceInsideCallArguments()
        {
            var expression = ParseSingleExpression("foo(1 + 2 * 3)");

            var call = expression as CallExpressionSyntax;
            Assert.That(call, Is.Not.Null);
            Assert.That(call.Target, Is.TypeOf<NameExpressionSyntax>());
            Assert.That(call.Arguments.Count, Is.EqualTo(1));

            var addition = call.Arguments[0] as BinaryExpressionSyntax;
            Assert.That(addition, Is.Not.Null);
            Assert.That(addition.OperatorToken.Kind, Is.EqualTo(SyntaxKind.PlusToken));

            var multiplication = addition.Right as BinaryExpressionSyntax;
            Assert.That(multiplication, Is.Not.Null);
            Assert.That(multiplication.OperatorToken.Kind, Is.EqualTo(SyntaxKind.StarToken));
        }

        [Test]
        public void Parser_KeepsMemberAccessAboveBinaryOperators()
        {
            var expression = ParseSingleExpression("x.y + z.w");

            var addition = expression as BinaryExpressionSyntax;
            Assert.That(addition, Is.Not.Null);
            Assert.That(addition.OperatorToken.Kind, Is.EqualTo(SyntaxKind.PlusToken));
            Assert.That(addition.Left, Is.TypeOf<MemberAccessExpressionSyntax>());
            Assert.That(addition.Right, Is.TypeOf<MemberAccessExpressionSyntax>());
        }

        [Test]
        public void Parser_RepresentsNegativeNumbersAsUnaryExpressions()
        {
            var expression = ParseSingleExpression("-1");

            var unary = expression as UnaryExpressionSyntax;
            Assert.That(unary, Is.Not.Null);
            Assert.That(unary.OperatorToken.Kind, Is.EqualTo(SyntaxKind.MinusToken));
            Assert.That(unary.Operand, Is.TypeOf<IntegerLiteralExpressionSyntax>());
        }

        [Test]
        public void Binder_BindsLogicalAndAsShortCircuitOperator()
        {
            var program = BindProgram(
                @"on Interact() {
  let a = true;
  let b = false;
  a && b;
}",
                CreateOperatorEnvironment());

            var binary = GetLastBoundExpression(program) as BoundBinaryExpression;
            Assert.That(binary, Is.Not.Null);
            Assert.That(binary.Operator.Kind, Is.EqualTo(BoundBinaryOperatorKind.LogicalAnd));
            Assert.That(binary.Operator.IsShortCircuit, Is.True);
            Assert.That(binary.Operator.ExternSignature, Is.Null);
            Assert.That(binary.Type, Is.EqualTo(TypeSymbol.Bool));
        }

        [Test]
        public void Binder_AllowsBoolBitwiseAnd()
        {
            var program = BindProgram(
                @"on Interact() {
  let a = true;
  let b = false;
  a & b;
}",
                CreateOperatorEnvironment());

            var binary = GetLastBoundExpression(program) as BoundBinaryExpression;
            Assert.That(binary, Is.Not.Null);
            Assert.That(binary.Operator.Kind, Is.EqualTo(BoundBinaryOperatorKind.BitwiseAnd));
            Assert.That(binary.Type, Is.EqualTo(TypeSymbol.Bool));
        }

        [Test]
        public void Binder_DesugarsCompoundAssignmentIntoBinaryExpression()
        {
            var program = BindProgram(
                @"on Interact() {
  let mut x = 1;
  x += 1;
}",
                CreateOperatorEnvironment());

            var assignment = GetLastBoundExpression(program) as BoundAssignmentExpression;
            Assert.That(assignment, Is.Not.Null);

            var binary = assignment.Expression as BoundBinaryExpression;
            Assert.That(binary, Is.Not.Null);
            Assert.That(binary.Operator.Kind, Is.EqualTo(BoundBinaryOperatorKind.Addition));
            Assert.That(binary.Left, Is.TypeOf<BoundNameExpression>());
            Assert.That(binary.Right, Is.TypeOf<BoundLiteralExpression>());
        }

        [TestCase(
            @"on Interact() {
  let a = ""a"";
  let b = ""b"";
  a + b;
}",
            "SBK2027")]
        [TestCase(
            @"on Interact() {
  let a = 1;
  let b = 1u32;
  a + b;
}",
            "SBK2027")]
        [TestCase(
            @"on Interact() {
  1 + 1.0f32;
}",
            "SBK2027")]
        [TestCase(
            @"on Interact() {
  !1;
}",
            "SBK2026")]
        [TestCase(
            @"on Interact() {
  ~true;
}",
            "SBK2026")]
        [TestCase(
            @"on Interact() {
  1 && true;
}",
            "SBK2030")]
        [TestCase(
            @"on Interact() {
  let mut x = 1;
  (x + 1) = 2;
}",
            "SBK2017")]
        [TestCase(
            @"on Interact() {
  let mut x = 1;
  (x + 1) += 2;
}",
            "SBK2029")]
        public void Binder_ReportsExpectedDiagnosticsForInvalidOperators(
            string source,
            string expectedDiagnosticCode)
        {
            var binder = CreateBinder(source, CreateOperatorEnvironment());

            Assert.That(
                ContainsDiagnosticCode(binder.Diagnostics.Diagnostics, expectedDiagnosticCode),
                Is.True,
                BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void Binder_ReportsAmbiguousOperatorWhenMultipleExternCandidatesMatch()
        {
            var binder = CreateBinder(
                @"on Interact() {
  let a = 1;
  let b = 2;
  a + b;
}",
                CreateOperatorEnvironment(includeAmbiguousIntAddition: true));

            Assert.That(
                ContainsDiagnosticCode(binder.Diagnostics.Diagnostics, "SBK2028"),
                Is.True,
                BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));
        }

        [Test]
        public void IrLowerer_LowersLogicalAndIntoConditionalBlocksWithoutEagerRhsCall()
        {
            var irProgram = LowerProgram(
                @"use Operators.Probe.Truthy;
on Interact() {
  let a = false;
  let b = a && Truthy();
}",
                CreateOperatorEnvironment());

            var module = irProgram.Modules[0];
            Assert.That(module.Blocks.Count, Is.GreaterThanOrEqualTo(4));

            var entryBlock = module.Blocks[0];
            Assert.That(entryBlock.Terminator, Is.TypeOf<IrConditionalJumpTerminator>());
            Assert.That(ContainsExternCall(entryBlock, TruthyExternSignature), Is.False);

            var rhsBlock = FindBlock(module, "__logical_rhs_");
            Assert.That(rhsBlock, Is.Not.Null);
            Assert.That(ContainsExternCall(rhsBlock, TruthyExternSignature), Is.True);

            var shortCircuitBlock = FindBlock(module, "__logical_short_");
            Assert.That(shortCircuitBlock, Is.Not.Null);
            Assert.That(ContainsBooleanConstantCopy(shortCircuitBlock, false), Is.True);
        }

        [Test]
        public void IrLowerer_LowersLogicalOrChainsIntoMultipleConditionalBranches()
        {
            var irProgram = LowerProgram(
                @"use Operators.Probe.Truthy;
on Interact() {
  let a = false;
  let b = true;
  let c = a && b || Truthy();
}",
                CreateOperatorEnvironment());

            var module = irProgram.Modules[0];

            Assert.That(CountConditionalTerminators(module), Is.GreaterThanOrEqualTo(2));
            Assert.That(CountBlocksWithPrefix(module, "__logical_rhs_"), Is.GreaterThanOrEqualTo(2));
            Assert.That(ContainsExternCall(module, TruthyExternSignature), Is.True);
        }

        private static CompilationUnitSyntax ParseCompilationUnit(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);
            return syntax;
        }

        private static ExpressionSyntax ParseSingleExpression(string expressionText)
        {
            var syntax = ParseCompilationUnit(
                $@"on Interact() {{
  {expressionText};
}}");

            var eventDeclaration = syntax.Members[0] as EventDeclarationSyntax;
            Assert.That(eventDeclaration, Is.Not.Null);

            var statement = eventDeclaration.Body.Statements[0] as ExpressionStatementSyntax;
            Assert.That(statement, Is.Not.Null);
            return statement.Expression;
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

        private static BoundExpression GetLastBoundExpression(BoundProgram program)
        {
            var statements = program.Events[0].Body.Statements;
            var statement = statements[statements.Count - 1] as BoundExpressionStatement;
            Assert.That(statement, Is.Not.Null);
            return statement.Expression;
        }

        private static IrProgram LowerProgram(
            string source,
            SobakasuCompilationEnvironment environment)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(parser.Diagnostics.Diagnostics, Is.Empty);

            var binder = new SobakasuBinder(environment);
            var boundProgram = binder.BindProgram(syntax);
            Assert.That(binder.Diagnostics.Diagnostics, Is.Empty, BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));

            var lowerer = new SobakasuIrLowerer();
            var irProgram = lowerer.Lower(boundProgram);
            Assert.That(lowerer.Diagnostics.Diagnostics, Is.Empty, BuildDiagnosticMessage(lowerer.Diagnostics.Diagnostics));
            return irProgram;
        }

        private static IrBasicBlock FindBlock(IrModule module, string labelPrefix)
        {
            foreach (var block in module.Blocks)
            {
                if (block.Label.StartsWith(labelPrefix, StringComparison.Ordinal))
                {
                    return block;
                }
            }

            return null;
        }

        private static bool ContainsExternCall(IrBasicBlock block, string externSignature)
        {
            if (block == null)
            {
                return false;
            }

            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrExternCallInstruction externCallInstruction &&
                    externCallInstruction.ExternSignature == externSignature)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsExternCall(IrModule module, string externSignature)
        {
            foreach (var block in module.Blocks)
            {
                if (ContainsExternCall(block, externSignature))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsBooleanConstantCopy(IrBasicBlock block, bool value)
        {
            if (block == null)
            {
                return false;
            }

            foreach (var instruction in block.Instructions)
            {
                if (instruction is IrCopyInstruction copyInstruction &&
                    copyInstruction.Source is IrConstantValue constantValue &&
                    constantValue.Type == TypeSymbol.Bool &&
                    Equals(constantValue.Value, value))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountConditionalTerminators(IrModule module)
        {
            var count = 0;
            foreach (var block in module.Blocks)
            {
                if (block.Terminator is IrConditionalJumpTerminator)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountBlocksWithPrefix(IrModule module, string labelPrefix)
        {
            var count = 0;
            foreach (var block in module.Blocks)
            {
                if (block.Label.StartsWith(labelPrefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
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

        private static SobakasuCompilationEnvironment CreateOperatorEnvironment(
            bool includeAmbiguousIntAddition = false)
        {
            var globalNamespace = new NamespaceSymbol("<global>", "");
            var typeSymbolsByClrType = CreatePrimitiveTypeMap();
            var typeSymbolsByQualifiedName = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
            {
                [TypeSymbol.Object.QualifiedName] = TypeSymbol.Object
            };

            var probeNamespace = globalNamespace.GetOrAddNamespace("Operators");
            var probeType = TypeSymbol.CreateNamed("Probe", "Operators.Probe");
            probeNamespace.AddType(probeType);
            typeSymbolsByQualifiedName[probeType.QualifiedName] = probeType;
            probeType.AddMethod(CreateExternMethod(
                probeType,
                "Truthy",
                GetHelperMethod(nameof(StubTruthy)),
                Array.Empty<TypeSymbol>(),
                TypeSymbol.Bool,
                TruthyExternSignature));

            var exposedSignatures = new List<string>();
            AddUnaryOperatorSignature(exposedSignatures, typeof(int), "op_UnaryPlus", typeof(int), typeof(int));
            AddUnaryOperatorSignature(exposedSignatures, typeof(int), "op_UnaryNegation", typeof(int), typeof(int));
            AddUnaryOperatorSignature(exposedSignatures, typeof(bool), "op_LogicalNot", typeof(bool), typeof(bool));
            AddUnaryOperatorSignature(exposedSignatures, typeof(int), "op_OnesComplement", typeof(int), typeof(int));

            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_Addition", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_Subtraction", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_Multiply", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_Division", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_Modulus", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_Equality", typeof(int), typeof(int), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_Inequality", typeof(int), typeof(int), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_LessThan", typeof(int), typeof(int), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_LessThanOrEqual", typeof(int), typeof(int), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_GreaterThan", typeof(int), typeof(int), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_GreaterThanOrEqual", typeof(int), typeof(int), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_BitwiseAnd", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_BitwiseOr", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_ExclusiveOr", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_LeftShift", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(int), "op_RightShift", typeof(int), typeof(int), typeof(int));
            AddBinaryOperatorSignature(exposedSignatures, typeof(bool), "op_Equality", typeof(bool), typeof(bool), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(bool), "op_Inequality", typeof(bool), typeof(bool), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(bool), "op_BitwiseAnd", typeof(bool), typeof(bool), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(bool), "op_BitwiseOr", typeof(bool), typeof(bool), typeof(bool));
            AddBinaryOperatorSignature(exposedSignatures, typeof(bool), "op_ExclusiveOr", typeof(bool), typeof(bool), typeof(bool));

            if (includeAmbiguousIntAddition)
            {
                exposedSignatures.Add("AltInt32.__op_Addition__SystemInt32_SystemInt32__SystemInt32");
            }

            var catalog = new ExternCatalog(
                globalNamespace,
                typeSymbolsByClrType,
                typeSymbolsByQualifiedName,
                new UdonExposedNodeCache(exposedSignatures));
            var compatibilitySymbols = new Dictionary<string, Symbol>(StringComparer.Ordinal);
            return new SobakasuCompilationEnvironment(catalog, compatibilitySymbols);
        }

        private static Dictionary<Type, TypeSymbol> CreatePrimitiveTypeMap()
        {
            return new Dictionary<Type, TypeSymbol>
            {
                [typeof(void)] = TypeSymbol.Void,
                [typeof(object)] = TypeSymbol.Object,
                [typeof(string)] = TypeSymbol.String,
                [typeof(bool)] = TypeSymbol.Bool,
                [typeof(char)] = TypeSymbol.Char,
                [typeof(sbyte)] = TypeSymbol.I8,
                [typeof(byte)] = TypeSymbol.U8,
                [typeof(short)] = TypeSymbol.I16,
                [typeof(ushort)] = TypeSymbol.U16,
                [typeof(int)] = TypeSymbol.I32,
                [typeof(uint)] = TypeSymbol.U32,
                [typeof(long)] = TypeSymbol.I64,
                [typeof(ulong)] = TypeSymbol.U64,
                [typeof(float)] = TypeSymbol.F32,
                [typeof(double)] = TypeSymbol.F64
            };
        }

        private static void AddUnaryOperatorSignature(
            ICollection<string> exposedSignatures,
            Type declaringType,
            string operatorName,
            Type operandType,
            Type resultType)
        {
            exposedSignatures.Add(BuildOperatorExternSignature(
                declaringType,
                operatorName,
                new[] { operandType },
                resultType));
        }

        private static void AddBinaryOperatorSignature(
            ICollection<string> exposedSignatures,
            Type declaringType,
            string operatorName,
            Type leftType,
            Type rightType,
            Type resultType)
        {
            exposedSignatures.Add(BuildOperatorExternSignature(
                declaringType,
                operatorName,
                new[] { leftType, rightType },
                resultType));
        }

        private static string BuildOperatorExternSignature(
            Type declaringType,
            string operatorName,
            IReadOnlyList<Type> parameterTypes,
            Type resultType)
        {
            var signature =
                $"{UdonExternSignatureFormatter.GetUdonTypeName(declaringType)}.__{operatorName}__";

            for (var index = 0; index < parameterTypes.Count; index++)
            {
                if (index > 0)
                {
                    signature += "_";
                }

                signature += UdonExternSignatureFormatter.GetUdonTypeName(parameterTypes[index]);
            }

            signature += $"__{UdonExternSignatureFormatter.GetUdonTypeName(resultType)}";
            return signature;
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

        private static bool StubTruthy()
        {
            return true;
        }

        private const string TruthyExternSignature =
            "OperatorsProbe.__Truthy__SystemBoolean";
    }
}
