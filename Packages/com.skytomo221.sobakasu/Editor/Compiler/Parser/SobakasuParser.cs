using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    public class SobakasuParser
    {
        private readonly ParserState _state;
        public DiagnosticBag Diagnostics => _state.Diagnostics;
        public SobakasuParser(SourceText text) : this(text, string.Empty) { }
        internal SobakasuParser(SourceText text, string sourcePath)
        {
            _state = new ParserState(text, sourcePath);
            _state.Utilities = new ParserUtilities(_state);
            _state.Modules = new ModuleParser(_state);
            _state.Expressions = new ExpressionParser(_state);
            _state.Patterns = new PatternParser(_state);
            _state.Types = new TypeParser(_state);
            _state.Statements = new StatementParser(_state);
            _state.Declarations = new DeclarationParser(_state);
        }
        public CompilationUnitSyntax ParseCompilationUnit() => _state.Declarations.ParseCompilationUnit();
    }
}
