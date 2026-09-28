using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    public sealed class DocumentationCommentSyntax : SyntaxNode
    {
        public string Markdown { get; }
        public TextSpan Span { get; }

        public DocumentationCommentSyntax(string markdown, TextSpan span)
        {
            Markdown = markdown ?? string.Empty;
            Span = span;
        }
    }
}
