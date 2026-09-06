using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BoundProgram : BoundNode
    {
        public IReadOnlyList<BoundConstantDeclaration> Constants { get; }
        public IReadOnlyList<BoundStateDeclaration> States { get; }
        public IReadOnlyList<BoundFunctionDeclaration> Functions { get; }
        public IReadOnlyList<BoundEventDeclaration> Events { get; }
        public IReadOnlyList<BoundNetworkReceiveDeclaration> NetworkReceivers { get; }

        public BoundProgram(
            IReadOnlyList<BoundConstantDeclaration> constants,
            IReadOnlyList<BoundStateDeclaration> states,
            IReadOnlyList<BoundFunctionDeclaration> functions,
            IReadOnlyList<BoundEventDeclaration> events,
            IReadOnlyList<BoundNetworkReceiveDeclaration> networkReceivers)
        {
            Constants = constants ?? throw new ArgumentNullException(nameof(constants));
            States = states ?? throw new ArgumentNullException(nameof(states));
            Functions = functions ?? throw new ArgumentNullException(nameof(functions));
            Events = events;
            NetworkReceivers = networkReceivers ??
                throw new ArgumentNullException(nameof(networkReceivers));
        }
    }
}
