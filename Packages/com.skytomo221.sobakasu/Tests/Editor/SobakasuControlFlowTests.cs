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

namespace Skytomo221.Sobakasu.Tests.Editor
{
    public class SobakasuControlFlowTests
    {
        private static readonly SobakasuCompilationEnvironment TestEnvironment =
            CreateTestEnvironment();

        [Test]
        public void Lexer_RecognizesControlKeywords()
        {
            var kinds = LexKinds(
                "if else while loop break continue redo");

            Assert.That(
                kinds,
                Is.EqualTo(new[]
                {
                    SyntaxKind.IfKeyword,
                    SyntaxKind.ElseKeyword,
                    SyntaxKind.WhileKeyword,
                    SyntaxKind.LoopKeyword,
                    SyntaxKind.BreakKeyword,
                    SyntaxKind.ContinueKeyword,
                    SyntaxKind.RedoKeyword,
                    SyntaxKind.EndOfFile
                }));
        }

        [Test]
        public void Lexer_DistinguishesCharacterLiteralAndLoopLabel()
        {
            var lexer = new SobakasuLexer(SourceText.From("'a' 'outer 'x'"));

            var character = lexer.Lex();
            var label = lexer.Lex();
            var secondCharacter = lexer.Lex();

            Assert.That(character.Kind, Is.EqualTo(SyntaxKind.CharacterLiteral));
            Assert.That(character.Value, Is.EqualTo('a'));
            Assert.That(label.Kind, Is.EqualTo(SyntaxKind.LabelIdentifier));
            Assert.That(label.Value, Is.EqualTo("outer"));
            Assert.That(secondCharacter.Kind, Is.EqualTo(SyntaxKind.CharacterLiteral));
            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty);
        }

        [TestCase("'ab'", "SBK0006")]
        [TestCase("'", "SBK0005")]
        public void Lexer_ReportsMalformedQuotedForms(
            string source,
            string expectedCode)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));
            lexer.Lex();

            Assert.That(
                ContainsDiagnosticCode(lexer.Diagnostics.Diagnostics, expectedCode),
                Is.True,
                BuildDiagnosticMessage(lexer.Diagnostics.Diagnostics));
        }

        [Test]
        public void Parser_ParsesOptionalConditionParenthesesElseIfAndTrailingValues()
        {
            var function = ParseSingleFunction(
                @"fn choose(a: bool, b: bool) -> i32 {
  if a {
    1
  } else if (b) {
    2
  } else {
    3
  }
}");

            var outer = function.Body.TrailingExpression as IfExpressionSyntax;
            Assert.That(outer, Is.Not.Null);
            Assert.That(outer.Condition, Is.TypeOf<NameExpressionSyntax>());
            Assert.That(outer.ThenBlock.TrailingExpression, Is.Not.Null);
            Assert.That(outer.ElseExpression, Is.TypeOf<IfExpressionSyntax>());

            var nested = (IfExpressionSyntax)outer.ElseExpression;
            Assert.That(nested.Condition, Is.TypeOf<ParenthesizedExpressionSyntax>());
            Assert.That(
                ((BlockExpressionSyntax)nested.ElseExpression).Block.TrailingExpression,
                Is.Not.Null);
        }

        [Test]
        public void Parser_ParsesLoopLabelDeclarationsAndReferences()
        {
            var function = ParseSingleFunction(
                @"fn run() {
  'outer: while true {
    loop {
      break 'outer;
    }
  }
}");

            var outer =
                (WhileExpressionSyntax)function.Body.TrailingExpression;
            Assert.That(outer.Label.LabelToken.Value, Is.EqualTo("outer"));

            var inner = (LoopExpressionSyntax)outer.Body.TrailingExpression;
            var breakStatement =
                (BreakStatementSyntax)inner.Body.Statements[0];
            Assert.That(breakStatement.Label.Value, Is.EqualTo("outer"));
            Assert.That(breakStatement.Expression, Is.Null);
        }

        [Test]
        public void Parser_RequiresBracesWithoutConsumingFollowingStatements()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"on interact() {
  if true
  extern UnityEngine.Debug.Log(""first"");
  extern UnityEngine.Debug.Log(""second"");
}"));
            var syntax = parser.ParseCompilationUnit();
            var @event = (EventDeclarationSyntax)syntax.Members[0];

            Assert.That(
                ContainsDiagnosticCode(parser.Diagnostics.Diagnostics, "SBK1007"),
                Is.True);
            Assert.That(@event.Body.Statements.Count, Is.EqualTo(3));
        }

        [Test]
        public void Parser_RecoversFromMissingLoopLabelColon()
        {
            var parser = new SobakasuParser(SourceText.From(
                @"on interact() {
  'outer while true {
    break;
  }
  extern UnityEngine.Debug.Log(""after"");
}

on start() {
}"));
            var syntax = parser.ParseCompilationUnit();

            Assert.That(
                ContainsDiagnosticCode(parser.Diagnostics.Diagnostics, "SBK1005"),
                Is.True);
            Assert.That(syntax.Members.Count, Is.EqualTo(2));
            Assert.That(
                ((EventDeclarationSyntax)syntax.Members[0]).Body.Statements.Count,
                Is.EqualTo(2));
        }

        [Test]
        public void Binder_UnifiesIfBranchesAndAdaptsNeverBranch()
        {
            var program = BindProgram(
                @"fn choose(enabled: bool) -> i32 {
  if enabled {
    10
  } else {
    loop {
    }
  }
}

on interact() {
  extern UnityEngine.Debug.Log(choose(true));
}");

            var returnStatement =
                (BoundReturnStatement)program.Functions[0].Body.Statements[0];
            var expression = (BoundIfExpression)returnStatement.Expression;

            Assert.That(expression.Type, Is.EqualTo(TypeSymbol.I32));
            Assert.That(expression.ElseExpression.Type, Is.EqualTo(TypeSymbol.Never));
        }

        [Test]
        public void Binder_BindsValueProducingLoopAndLabeledOuterBreak()
        {
            var program = BindProgram(
                @"fn search(found: bool) -> i32 {
  'search: loop {
    loop {
      if found {
        break 'search 42;
      }
      continue;
    }
  }
}

on interact() {
  extern UnityEngine.Debug.Log(search(true));
}");

            var returnStatement =
                (BoundReturnStatement)program.Functions[0].Body.Statements[0];
            var outerLoop = (BoundLoopExpression)returnStatement.Expression;
            var innerLoop =
                (BoundLoopExpression)outerLoop.Body.TrailingExpression;

            Assert.That(outerLoop.Type, Is.EqualTo(TypeSymbol.I32));
            Assert.That(innerLoop.Type, Is.EqualTo(TypeSymbol.Never));
        }

        [TestCase(
            "on interact() { if 1 { } }",
            "SBK2047")]
        [TestCase(
            "on interact() { let value = if true { 1 }; }",
            "SBK2048")]
        [TestCase(
            "on interact() { let value = if true { 1 } else { \"x\" }; }",
            "SBK2049")]
        [TestCase(
            "on interact() { while true { break 1; } }",
            "SBK2050")]
        [TestCase(
            "on interact() { loop { if true { break; } break 1; } }",
            "SBK2051")]
        [TestCase(
            "on interact() { loop { if true { break 1; } break \"x\"; } }",
            "SBK2052")]
        [TestCase(
            "on interact() { break; }",
            "SBK2053")]
        [TestCase(
            "on interact() { continue; }",
            "SBK2053")]
        [TestCase(
            "on interact() { redo; }",
            "SBK2053")]
        [TestCase(
            "on interact() { loop { break 'missing; } }",
            "SBK2054")]
        [TestCase(
            "on interact() { 'same: while true { 'same: loop { break; } } }",
            "SBK2055")]
        public void Binder_ReportsControlFlowDiagnostics(
            string source,
            string expectedCode)
        {
            var binder = CreateBinder(source);

            Assert.That(
                ContainsDiagnosticCode(binder.Diagnostics.Diagnostics, expectedCode),
                Is.True,
                BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));
        }

        [TestCase(
            "on interact() { continue 1; }",
            "SBK1008")]
        [TestCase(
            "on interact() { redo \"again\"; }",
            "SBK1008")]
        [TestCase(
            "on interact() { 'name: if true { } }",
            "SBK1006")]
        [TestCase(
            "on interact() { loop { break 1 'wrong; } }",
            "SBK1009")]
        public void Parser_ReportsInvalidControlSyntax(
            string source,
            string expectedCode)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            parser.ParseCompilationUnit();

            Assert.That(
                ContainsDiagnosticCode(parser.Diagnostics.Diagnostics, expectedCode),
                Is.True,
                BuildDiagnosticMessage(parser.Diagnostics.Diagnostics));
        }

        [Test]
        public void IrLowerer_UsesDifferentWhileTargetsForContinueAndRedo()
        {
            var ir = LowerProgram(
                @"on interact() {
  'outer: while true {
    if false {
      continue 'outer;
    }
    redo 'outer;
  }
}");
            var module = ir.Modules[0];
            var conditionLabel = module.Blocks
                .Single(block => block.Label.Contains("while_condition"))
                .Label;
            var bodyLabel = module.Blocks
                .Single(block => block.Label.Contains("while_body"))
                .Label;
            var jumpTargets = module.Blocks
                .Select(block => block.Terminator)
                .OfType<IrJumpTerminator>()
                .Select(terminator => terminator.TargetLabel)
                .ToArray();

            Assert.That(jumpTargets, Does.Contain(conditionLabel));
            Assert.That(jumpTargets, Does.Contain(bodyLabel));
            Assert.That(conditionLabel, Is.Not.EqualTo(bodyLabel));
        }

        [Test]
        public void IrLowerer_LabeledBreakTargetsOuterLoopResultSlot()
        {
            var ir = LowerProgram(
                @"on interact() {
  let answer = 'outer: loop {
    loop {
      break 'outer 42;
    }
  };
  extern UnityEngine.Debug.Log(answer);
}");
            var module = ir.Modules[0];
            var outerExit = module.Blocks
                .First(block => block.Label.Contains("loop_exit"))
                .Label;

            Assert.That(
                module.Blocks
                    .Select(block => block.Terminator)
                    .OfType<IrJumpTerminator>()
                    .Any(terminator => terminator.TargetLabel == outerExit),
                Is.True);
            Assert.That(
                module.Blocks
                    .SelectMany(block => block.Instructions)
                    .OfType<IrCopyInstruction>()
                    .Any(copy => copy.Target is IrTemporaryStorage),
                Is.True);
        }

        [Test]
        public void CompileToUasm_EmitsValueIfMergeSlotAndBranches()
        {
            var result = CompileControlToUasm(
                @"on interact() {
  let value = if true {
    10
  } else {
    20
  };
  extern UnityEngine.Debug.Log(value);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("__if_then_"));
            Assert.That(result.Uasm, Does.Contain("__if_else_"));
            Assert.That(result.Uasm, Does.Contain("__if_merge_"));
            Assert.That(result.Uasm, Does.Contain("JUMP_IF_FALSE"));
            Assert.That(result.Uasm, Does.Contain("__temp_"));
        }

        [Test]
        public void CompileToUasm_EmitsLoopResultSlotAndEvaluatesBreakValueOnce()
        {
            var result = CompileControlToUasm(
                @"on interact() {
  let mut count = 0;
  let value = loop {
    break count += 1;
  };
  extern UnityEngine.Debug.Log(value);
}");

            Assert.That(result.Success, Is.True, result.ErrorText);
            Assert.That(result.Uasm, Does.Contain("__loop_body_"));
            Assert.That(result.Uasm, Does.Contain("__loop_exit_"));
            Assert.That(
                CountOccurrences(result.Uasm, "SystemInt32.__op_Addition__"),
                Is.EqualTo(1));
        }

        [Test]
        public void CompileToUasm_NeverBranchDoesNotJumpToIfMerge()
        {
            var ir = LowerProgram(
                @"on interact() {
  let value = if true {
    10
  } else {
    loop {
    }
  };
  extern UnityEngine.Debug.Log(value);
}");
            var module = ir.Modules[0];
            var mergeLabel = module.Blocks
                .Single(block => block.Label.Contains("if_merge"))
                .Label;
            var infiniteLoopBlock = module.Blocks
                .Single(block => block.Label.Contains("loop_body"));

            Assert.That(
                ((IrJumpTerminator)infiniteLoopBlock.Terminator).TargetLabel,
                Is.EqualTo(infiniteLoopBlock.Label));
            Assert.That(
                ((IrJumpTerminator)infiniteLoopBlock.Terminator).TargetLabel,
                Is.Not.EqualTo(mergeLabel));
        }

        private static IReadOnlyList<SyntaxKind> LexKinds(string source)
        {
            var lexer = new SobakasuLexer(SourceText.From(source));
            var kinds = new List<SyntaxKind>();
            SyntaxToken token;
            do
            {
                token = lexer.Lex();
                kinds.Add(token.Kind);
            }
            while (token.Kind != SyntaxKind.EndOfFile);

            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty);
            return kinds;
        }

        private static FunctionDeclarationSyntax ParseSingleFunction(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(
                parser.Diagnostics.Diagnostics,
                Is.Empty,
                BuildDiagnosticMessage(parser.Diagnostics.Diagnostics));
            Assert.That(syntax.Members.Count, Is.EqualTo(1));
            return (FunctionDeclarationSyntax)syntax.Members[0];
        }

        private static SobakasuBinder CreateBinder(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(
                parser.Diagnostics.Diagnostics,
                Is.Empty,
                BuildDiagnosticMessage(parser.Diagnostics.Diagnostics));

            var binder = new SobakasuBinder(TestEnvironment);
            binder.BindProgram(syntax);
            return binder;
        }

        private static BoundProgram BindProgram(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            Assert.That(
                parser.Diagnostics.Diagnostics,
                Is.Empty,
                BuildDiagnosticMessage(parser.Diagnostics.Diagnostics));

            var binder = new SobakasuBinder(TestEnvironment);
            var program = binder.BindProgram(syntax);
            Assert.That(
                binder.Diagnostics.Diagnostics,
                Is.Empty,
                BuildDiagnosticMessage(binder.Diagnostics.Diagnostics));
            return program;
        }

        private static IrProgram LowerProgram(string source)
        {
            var program = BindProgram(source);
            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);
            Assert.That(
                lowerer.Diagnostics.Diagnostics,
                Is.Empty,
                BuildDiagnosticMessage(lowerer.Diagnostics.Diagnostics));
            return ir;
        }

        private static SobakasuCompiler.CompileResult CompileControlToUasm(
            string source)
        {
            var text = SourceText.From(source);
            var diagnostics = new DiagnosticBag();
            var parser = new SobakasuParser(text);
            var syntax = parser.ParseCompilationUnit();
            diagnostics.AddRange(parser.Diagnostics);

            var binder = new SobakasuBinder(TestEnvironment);
            var program = binder.BindProgram(syntax);
            diagnostics.AddRange(binder.Diagnostics);
            if (diagnostics.HasErrors)
            {
                return SobakasuCompiler.CompileResult.Fail(
                    BuildDiagnosticMessage(diagnostics.Diagnostics),
                    diagnostics.Diagnostics);
            }

            var desugarer = new SobakasuDesugarer();
            program = desugarer.Desugar(program);
            diagnostics.AddRange(desugarer.Diagnostics);

            var lowerer = new SobakasuIrLowerer();
            var ir = lowerer.Lower(program);
            diagnostics.AddRange(lowerer.Diagnostics);
            if (diagnostics.HasErrors)
            {
                return SobakasuCompiler.CompileResult.Fail(
                    BuildDiagnosticMessage(diagnostics.Diagnostics),
                    diagnostics.Diagnostics);
            }

            var optimizer = new SobakasuOptimizer();
            ir = optimizer.Optimize(ir);
            var assembler = new SobakasuUasmAssembler();
            var uasm = assembler.Assemble(ir);
            diagnostics.AddRange(assembler.Diagnostics);
            if (diagnostics.HasErrors)
            {
                return SobakasuCompiler.CompileResult.Fail(
                    BuildDiagnosticMessage(diagnostics.Diagnostics),
                    diagnostics.Diagnostics);
            }

            return SobakasuCompiler.CompileResult.Ok(
                uasm,
                assembler.HeapPatches,
                diagnostics.Diagnostics);
        }

        private static SobakasuCompilationEnvironment CreateTestEnvironment()
        {
            const string additionSignature =
                "SystemInt32.__op_Addition__SystemInt32_SystemInt32__SystemInt32";
            var globalNamespace = new NamespaceSymbol("<global>", "");
            var unityEngineNamespace =
                globalNamespace.GetOrAddNamespace("UnityEngine");
            var debugType = TypeSymbol.CreateNamed(
                "Debug",
                "UnityEngine.Debug");
            var logMethod = new ExternMethodSymbol(
                "Log",
                debugType,
                new[]
                {
                    new ParameterSymbol("value", TypeSymbol.Object, 0)
                },
                TypeSymbol.Unit,
                typeof(SobakasuControlFlowTests).GetMethod(
                    nameof(TestLog),
                    BindingFlags.Static | BindingFlags.NonPublic),
                "UnityEngineDebug.__Log__SystemObject__SystemVoid");
            debugType.AddMethod(logMethod);
            unityEngineNamespace.AddType(debugType);

            var clrTypes = new Dictionary<System.Type, TypeSymbol>
            {
                [typeof(void)] = TypeSymbol.Unit,
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
                [typeof(double)] = TypeSymbol.F64,
                [typeof(string)] = TypeSymbol.String,
                [typeof(object)] = TypeSymbol.Object
            };
            var typesByName = clrTypes.Values
                .Distinct()
                .ToDictionary(type => type.QualifiedName, type => type);
            typesByName[debugType.QualifiedName] = debugType;
            var catalog = new ExternCatalog(
                globalNamespace,
                clrTypes,
                typesByName,
                new UdonExposedNodeCache(new[] { additionSignature }));
            return new SobakasuCompilationEnvironment(catalog);
        }

        private static void TestLog(object value)
        {
        }

        private static bool ContainsDiagnosticCode(
            IReadOnlyList<Diagnostic> diagnostics,
            string expectedCode)
        {
            return diagnostics.Any(diagnostic => diagnostic.Code == expectedCode);
        }

        private static string BuildDiagnosticMessage(
            IReadOnlyList<Diagnostic> diagnostics)
        {
            return string.Join(
                "\n",
                diagnostics.Select(
                    diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        }

        private static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var position = 0;
            while ((position = text.IndexOf(
                value,
                position,
                System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                position += value.Length;
            }

            return count;
        }
    }
}
