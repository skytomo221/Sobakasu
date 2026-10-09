using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal enum BehaviorCallableInvocationForm
    {
        Unqualified,
        BehaviorAssociated,
        BehaviorExplicitState,
        StateReceiver,
    }

    internal sealed class BehaviorCallableInvocation
    {
        internal string Name { get; }
        internal TextSpan NameSpan { get; }
        internal BehaviorCallableInvocationForm Form { get; }
        internal bool RequiresStateCapability { get; }
        internal TextSpan StateCapabilitySpan { get; }
        internal IReadOnlyList<ExpressionSyntax> RuntimeArguments { get; }

        internal BehaviorCallableInvocation(
            string name,
            TextSpan nameSpan,
            BehaviorCallableInvocationForm form,
            bool requiresStateCapability,
            TextSpan stateCapabilitySpan,
            IReadOnlyList<ExpressionSyntax> runtimeArguments)
        {
            Name = name;
            NameSpan = nameSpan;
            Form = form;
            RequiresStateCapability = requiresStateCapability;
            StateCapabilitySpan = stateCapabilitySpan;
            RuntimeArguments = runtimeArguments;
        }
    }
}
