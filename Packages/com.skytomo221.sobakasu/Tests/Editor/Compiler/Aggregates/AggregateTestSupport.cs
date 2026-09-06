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

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class AggregateTestSupport
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

            Assert.That(lexer.Diagnostics.Diagnostics, Is.Empty,
                Format(lexer.Diagnostics.Diagnostics));
            return tokens;
        }
        internal static (BoundProgram Program, IReadOnlyList<Diagnostic> Diagnostics) Bind(
            string source)
        {
            var parser = new SobakasuParser(SourceText.From(source));
            var syntax = parser.ParseCompilationUnit();
            if (parser.Diagnostics.HasErrors)
                return (null, parser.Diagnostics.Diagnostics);

            var binder = new SobakasuBinder();
            var program = binder.BindProgram(syntax);
            return (program, binder.Diagnostics.Diagnostics);
        }
        internal static List<string> StateNames(IrProgram program)
        {
            var result = new List<string>();
            foreach (var state in program.States)
                result.Add(state.Name);
            return result;
        }
        internal static List<string> StateWriteNames(IrModule module)
        {
            var result = new List<string>();
            foreach (var block in module.Blocks)
                foreach (var instruction in block.Instructions)
                {
                    if (instruction is IrCopyInstruction copy &&
                        copy.Target is IrStateStorage state)
                    {
                        result.Add(state.State.Name);
                    }
                }
            return result;
        }
        internal static HeapPatchEntry FindPatch(
            IReadOnlyList<HeapPatchEntry> patches,
            string symbol)
        {
            foreach (var patch in patches)
            {
                if (patch.SymbolName == symbol)
                    return patch;
            }
            return null;
        }
        internal static bool ContainsCode(
            IReadOnlyList<Diagnostic> diagnostics,
            string expectedCode)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Code == expectedCode)
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
        internal static void AssertHeapValue(
            SobakasuProgramAsset asset,
            string symbol,
            object expected)
        {
            var program = asset.GetRealProgram();
            var address = program.SymbolTable.GetAddressFromSymbol(symbol);
            Assert.That(program.Heap.GetHeapVariable(address), Is.EqualTo(expected));
        }
    }
}
