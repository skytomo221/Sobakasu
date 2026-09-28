using System;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal enum SymbolKind
    {
        Module,
        Namespace,
        Type,
        MethodGroup,
        Method,
        Event,
        NetworkReceive,
        Function,
        FunctionGroup,
        Parameter,
        Local,
        State,
        Constant,
        AggregateField,
        EnumVariant
    }

    internal abstract class Symbol
    {
        protected Symbol(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
        public abstract SymbolKind Kind { get; }
    }

    internal interface IDocumentableSymbol
    {
        DocumentationComment Documentation { get; set; }
    }

    internal sealed class DocumentationComment
    {
        public string Markdown { get; }
        public TextSpan SourceSpan { get; }

        public DocumentationComment(string markdown, TextSpan sourceSpan)
        {
            Markdown = markdown ?? string.Empty;
            SourceSpan = sourceSpan;
        }

        internal static DocumentationComment FromSyntax(
            DocumentationCommentSyntax syntax)
        {
            return syntax == null
                ? null
                : new DocumentationComment(syntax.Markdown, syntax.Span);
        }
    }
}
