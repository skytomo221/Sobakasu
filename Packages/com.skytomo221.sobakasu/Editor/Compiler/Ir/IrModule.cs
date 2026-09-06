using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;

namespace Skytomo221.Sobakasu.Compiler.Ir
{
    internal sealed class IrModule
    {
        public string Name { get; }
        public string ExportName { get; }
        public IReadOnlyList<ParameterSymbol> Parameters { get; }
        public string ReturnValueStorageName { get; }
        public IReadOnlyList<IrBasicBlock> Blocks { get; }

        public IrModule(BoundEventSymbol eventSymbol, IReadOnlyList<IrBasicBlock> blocks)
        {
            if (eventSymbol == null) throw new ArgumentNullException(nameof(eventSymbol));
            Name = eventSymbol.SourceName; ExportName = eventSymbol.UdonName;
            Parameters = eventSymbol.Parameters; ReturnValueStorageName = eventSymbol.ReturnValueStorageName;
            Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
        }

        public IrModule(NetworkReceiveSymbol receiveSymbol, IReadOnlyList<IrBasicBlock> blocks)
        {
            if (receiveSymbol == null) throw new ArgumentNullException(nameof(receiveSymbol));
            Name = receiveSymbol.Name; ExportName = receiveSymbol.ExportName;
            var parameters = new List<ParameterSymbol>(receiveSymbol.PhysicalParameters.Count);
            foreach (var parameter in receiveSymbol.PhysicalParameters) parameters.Add(parameter.PhysicalParameter);
            Parameters = parameters; ReturnValueStorageName = null;
            Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
        }
    }
}
