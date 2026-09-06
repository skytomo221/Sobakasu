using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class MemberResolver : BinderComponent
    {
        internal MemberResolver(BindingSession session) : base(session)
        {
        }

        internal Symbol LookupMember(BoundExpression receiver, string memberName, TextSpan span, out bool diagnosticReported)
        {
            diagnosticReported = false;
            var receiverSymbol = Session.NameResolver.GetReferencedSymbol(receiver);
            if (receiverSymbol is ModuleSymbol moduleSymbol)
            {
                return Session.NameResolver.LookupModuleMember(moduleSymbol, memberName, span, out diagnosticReported);
            }

            if (receiverSymbol is TypeSymbol explicitTypeSymbol)
            {
                Session.GenericInstantiation.EnsureConstructedGenericMethods(explicitTypeSymbol);
                if (Session.Declarations.MethodGroupsByType.TryGetValue(explicitTypeSymbol, out var typeGroups) && typeGroups.TryGetValue(memberName, out var explicitMethodGroup))
                {
                    return explicitMethodGroup;
                }
            }

            Session.GenericInstantiation.EnsureConstructedGenericMethods(receiver.Type);
            if (Session.Declarations.MethodGroupsByType.TryGetValue(receiver.Type, out var groups) && groups.TryGetValue(memberName, out var methods))
            {
                return methods;
            }

            return null;
        }
    }
}
