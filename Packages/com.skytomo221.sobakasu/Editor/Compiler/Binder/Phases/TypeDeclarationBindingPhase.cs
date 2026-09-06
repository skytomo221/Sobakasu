using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class TypeDeclarationBindingPhase : BinderComponent
    {
        internal TypeDeclarationBindingPhase(BindingSession session) : base(session)
        {
        }

        internal void Execute(StandardLibraryModuleGraph graph)
        {
            foreach (var module in graph.Modules)
            {
                Session.ModuleResolver.SetCurrentModule(module, includeFunctions: false);
                foreach (var member in module.Syntax.Members)
                {
                    if (member is StructDeclarationSyntax structDeclaration)
                        Session.AggregateDeclarationBinder.BindStructDeclaration(structDeclaration);
                    else if (member is EnumDeclarationSyntax enumDeclaration)
                        Session.AggregateDeclarationBinder.BindEnumDeclaration(enumDeclaration);
                }
            }

            Session.AggregateDependencyValidator.ValidateAggregateDependencies();
        }
    }
}
