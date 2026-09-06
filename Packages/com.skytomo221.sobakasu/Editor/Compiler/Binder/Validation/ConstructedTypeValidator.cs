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
    internal sealed class ConstructedTypeValidator : BinderComponent
    {
        internal ConstructedTypeValidator(BindingSession session) : base(session)
        {
        }

        internal void ValidateConstructedAggregateTypes()
        {
            var validated = new HashSet<TypeSymbol>();
            foreach (var definition in Session.Declarations.AggregateTypesBySyntax.Values)
            {
                foreach (var constructed in definition.ConstructedGenericTypes)
                {
                    if (constructed.ContainsGenericParameters || !validated.Add(constructed))
                        continue;
                    foreach (var leaf in AggregateLayout.GetLeaves(constructed))
                    {
                        if (leaf.Type.ContainsGenericParameters)
                        {
                            Session.Diagnostics.ReportOpenGenericType(new TextSpan(0, 0), constructed.Name);
                            continue;
                        }

                        var supported = leaf.Type.TypeKind == TypeKind.Array ? Session.Environment.ExternCatalog.TryGetArrayIntrinsics(leaf.Type, out _, out _) : leaf.Type != TypeSymbol.Unit && leaf.Type != TypeSymbol.Never && Session.Environment.ExternCatalog.TryGetClrType(leaf.Type, out _);
                        if (!supported)
                        {
                            Session.Diagnostics.ReportUnsupportedAggregateLeafAbi(new TextSpan(0, 0), constructed.Name, leaf.PathText, leaf.Type.Name);
                        }
                    }
                }
            }
        }
    }
}
