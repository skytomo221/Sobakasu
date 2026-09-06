using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class LanguageItemBindingPhase : BinderComponent
    {
        internal LanguageItemBindingPhase(BindingSession session) : base(session)
        {
        }

        internal void Execute(StandardLibraryModuleGraph graph)
        {
            foreach (var module in graph.Modules)
            {
                foreach (var member in module.Syntax.Members)
                {
                    var metadata = GetLanguageItem(member);
                    if (metadata?.Item.Value is not string item)
                        continue;
                    if (!LanguageItemNames.IsKnown(item))
                    {
                        Session.Diagnostics.ReportUnknownLanguageItem(metadata.Item.Span, item);
                        continue;
                    }

                    TypeSymbol type = null;
                    if (member is StructDeclarationSyntax or EnumDeclarationSyntax)
                    {
                        Session.Declarations.AggregateTypesBySyntax.TryGetValue(member, out type);
                    }
                    else if (member is ImplDeclarationSyntax impl && impl.IsExternalBinding)
                    {
                        Session.Declarations.ExternalTypesBySyntax.TryGetValue(impl, out type);
                    }
                    else
                    {
                        Session.Diagnostics.ReportInvalidLanguageItemDeclaration(
                            metadata.LangKeyword.Span,
                            item);
                        continue;
                    }

                    if (type == null || type == TypeSymbol.Error)
                        continue;
                    if (Session.LanguageItems.Types.TryGetValue(item, out var existing))
                    {
                        Session.Diagnostics.ReportDuplicateLanguageItem(
                            metadata.Item.Span,
                            item,
                            existing.Name);
                        continue;
                    }

                    Session.LanguageItems.Types.Add(item, type);
                }
            }
        }

        private static LanguageItemSyntax GetLanguageItem(MemberSyntax member)
        {
            return member switch
            {
                StructDeclarationSyntax declaration => declaration.LanguageItem,
                EnumDeclarationSyntax declaration => declaration.LanguageItem,
                ImplDeclarationSyntax declaration => declaration.LanguageItem,
                _ => null
            };
        }
    }
}
