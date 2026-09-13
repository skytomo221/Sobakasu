using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Ir;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.IrLowerer
{
    internal sealed class StoragePlanner : LoweringComponent
    {
        public StoragePlanner(LoweringSession session) : base(session) { }

        internal IReadOnlyDictionary<ParameterSymbol, IrStorage>
            CreateNetworkReceiveParameterStorage(NetworkReceiveSymbol receiver)
        {
            var result = new Dictionary<ParameterSymbol, IrStorage>();
            foreach (var logical in receiver.Parameters)
            {
                var leaves = new List<IrStorage>();
                foreach (var physical in receiver.PhysicalParameters)
                {
                    if (ReferenceEquals(physical.LogicalParameter, logical))
                        leaves.Add(new IrParameterStorage(physical.PhysicalParameter));
                }

                if (logical.Type.UsesFlattenedAggregateStorage &&
                    logical.Type.AggregateKind == UserAggregateKind.Struct)
                {
                    result[logical] = Session.AggregateStorageLowerer.CreateStorage(
                        logical.Type,
                        leaves);
                }
                else if (leaves.Count > 0)
                {
                    result[logical] = leaves[0];
                }
            }

            return result;
        }
        internal List<StateVariableSymbol> Plan(
          IReadOnlyList<BoundStateDeclaration> declarations)
        {
            var states = new List<StateVariableSymbol>();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var declaration in declarations)
            {
                if (!Session.AggregateStorageLowerer.IsAggregateStorageType(declaration.StateSymbol.Type))
                {
                    usedNames.Add(declaration.StateSymbol.Name);
                }
            }

            foreach (var declaration in declarations)
            {
                var state = declaration.StateSymbol;
                if (!Session.AggregateStorageLowerer.IsAggregateStorageType(state.Type))
                {
                    states.Add(state);
                    Session.StateStorageMap[state] = new IrStateStorage(state);
                    continue;
                }

                var descriptors = AggregateLayout.GetLeaves(state.Type);
                var leaves = new List<IrStorage>(descriptors.Count);
                for (var index = 0; index < descriptors.Count; index++)
                {
                    var pathName = string.Join("__", descriptors[index].Path);
                    var publicName = string.IsNullOrEmpty(pathName)
                        ? state.Name
                        : $"{state.Name}__{pathName}";
                    var candidate = publicName;
                    var suffix = 0;
                    while (!usedNames.Add(candidate))
                        candidate = $"{publicName}__aggregate_{++suffix}";
                    publicName = candidate;

                    var leafState = new StateVariableSymbol(
                        publicName,
                        descriptors[index].Type,
                        state.IsPublic,
                        state.SynchronizationMode,
                        state.InitialValue is AggregateConstantValue constant && index < constant.Leaves.Count
                            ? constant.Leaves[index]
                            : null,
                        state.DeclarationSpan,
                        state.InitializerSpan,
                        states.Count);
                    states.Add(leafState);
                    leaves.Add(new IrStateStorage(leafState));
                }

                Session.StateStorageMap[state] = Session.AggregateStorageLowerer.CreateStorage(
                    state.Type,
                    leaves);
            }

            return states;
        }

    }
}
