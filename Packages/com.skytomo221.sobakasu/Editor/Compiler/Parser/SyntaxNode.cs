namespace Skytomo221.Sobakasu.Compiler.Parser
{
    public abstract class SyntaxNode
    {
    }

    internal interface IDocumentableSyntax
    {
        DocumentationCommentSyntax Documentation { get; set; }
    }
}
