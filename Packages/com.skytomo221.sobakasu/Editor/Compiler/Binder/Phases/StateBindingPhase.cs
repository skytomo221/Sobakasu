using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class StateBindingPhase : BinderComponent
    {
        internal StateBindingPhase(BindingSession session) : base(session)
        {
        }

        internal IReadOnlyList<BoundStateDeclaration> Execute(StandardLibraryModule entryModule)
        {
            Session.ModuleResolver.SetCurrentModule(entryModule, includeFunctions: true);
            var members = new List<StateDeclarationSyntax>();
            var sawBlock = false;
            foreach (var member in entryModule.Syntax.Members)
            {
                if (member is not StateBlockDeclarationSyntax block)
                    continue;
                if (sawBlock)
                    Session.Diagnostics.ReportDuplicateStateBlock(block.StateKeyword.Span);
                sawBlock = true;
                members.AddRange(block.Members);
            }
            var declarations = Session.StateDeclarationBinder.CollectStateDeclarations(members);
            return Session.StateDeclarationBinder.BindStateDeclarations(declarations);
        }
    }
}
