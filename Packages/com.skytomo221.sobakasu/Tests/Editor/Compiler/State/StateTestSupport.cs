using System;
using System.Collections.Generic;
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

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class StateTestSupport
    {
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

            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty);
            return tokens;
        }
        internal static (BoundProgram Program, IReadOnlyList<Diagnostic> Diagnostics) Bind(string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            if (parser.Diagnostics.HasErrors)
                return (null, parser.Diagnostics.Diagnostics);

            var binder = new SobakasuBinder();
            var program = binder.BindProgram(syntax);
            return (program, binder.Diagnostics.Diagnostics);
        }
        internal static int CountStateCopies(IrProgram program)
        {
            var count = 0;
            foreach (var module in program.Modules)
                foreach (var block in module.Blocks)
                    foreach (var instruction in block.Instructions)
                    {
                        if (instruction is IrCopyInstruction copy && copy.Target is IrStateStorage)
                            count++;
                    }

            return count;
        }
        internal static int CountStateValues(IrProgram program)
        {
            var count = 0;
            foreach (var module in program.Modules)
                foreach (var block in module.Blocks)
                    foreach (var instruction in block.Instructions)
                    {
                        if (instruction is IrCopyInstruction copy && copy.Source is IrStateStorage)
                            count++;

                        if (instruction is IrExternCallInstruction call)
                            foreach (var argument in call.Arguments)
                            {
                                if (argument is IrStateStorage)
                                    count++;
                            }
                    }

            return count;
        }
        internal static bool ContainsIrConstant(IrProgram program, object value)
        {
            foreach (var module in program.Modules)
                foreach (var block in module.Blocks)
                    foreach (var instruction in block.Instructions)
                    {
                        if (instruction is IrCopyInstruction copy &&
                            copy.Source is IrConstantValue constant &&
                            Equals(constant.Value, value))
                        {
                            return true;
                        }

                        if (instruction is IrExternCallInstruction call)
                            foreach (var argument in call.Arguments)
                            {
                                if (argument is IrConstantValue argumentConstant &&
                                    Equals(argumentConstant.Value, value))
                                {
                                    return true;
                                }
                            }
                    }

            return false;
        }
        internal static int CountGlobalInitializerPatches(
            IReadOnlyList<HeapPatchEntry> patches)
        {
            var count = 0;
            foreach (var patch in patches)
            {
                if (patch.Kind == HeapPatchKind.GlobalInitializer)
                    count++;
            }
            return count;
        }
        internal static string FormatHeapPatches(IReadOnlyList<HeapPatchEntry> patches)
        {
            var entries = new List<string>();
            foreach (var patch in patches)
                entries.Add($"{patch.Kind}:{patch.SymbolName}");
            return string.Join(", ", entries);
        }
        internal static HeapPatchEntry FindStatePatch(
            IReadOnlyList<HeapPatchEntry> patches,
            string symbolFragment)
        {
            foreach (var patch in patches)
            {
                if (patch.Kind == HeapPatchKind.GlobalInitializer &&
                    patch.SymbolName.IndexOf(symbolFragment, StringComparison.Ordinal) >= 0)
                {
                    return patch;
                }
            }

            return null;
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
        internal static bool ContainsCode(IReadOnlyList<Diagnostic> diagnostics, string code)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == code)
                    return true;
            }

            return false;
        }
        internal static string Format(IReadOnlyList<Diagnostic> diagnostics)
        {
            var lines = new List<string>();
            foreach (var diagnostic in diagnostics)
                lines.Add($"{diagnostic.Code}: {diagnostic.Message}");
            return string.Join("\n", lines);
        }
        internal static void AssertProgramState(
            SobakasuProgramAsset asset,
            string symbol,
            object expectedValue,
            string expectedInterpolation)
        {
            var program = asset.GetRealProgram();
            Assert.That(program, Is.Not.Null);
            Assert.That(program.SymbolTable.HasExportedSymbol(symbol), Is.False);
            var address = program.SymbolTable.GetAddressFromSymbol(symbol);
            Assert.That(program.Heap.GetHeapVariable(address), Is.EqualTo(expectedValue));

            var sync = program.SyncMetadataTable.GetSyncMetadataFromSymbol(symbol);
            Assert.That(sync, Is.Not.Null);
            Assert.That(sync.Properties.Count, Is.GreaterThan(0));
            Assert.That(sync.Properties[0].InterpolationAlgorithm.ToString(),
                Is.EqualTo(expectedInterpolation));
        }
    }
}
