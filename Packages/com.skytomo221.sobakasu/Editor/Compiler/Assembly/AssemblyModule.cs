using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Assembly
{
  internal sealed class AssemblyDataSlot
  {
    public string Name { get; }
    public string TypeName { get; }
    public string InitialValue { get; }
    public bool IsExported { get; }
    public string SyncMode { get; }

    public AssemblyDataSlot(
        string name,
        string typeName,
        string initialValue,
        bool isExported = false,
        string syncMode = null)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
      TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
      InitialValue = initialValue ?? throw new ArgumentNullException(nameof(initialValue));
      IsExported = isExported;
      SyncMode = syncMode;
    }
  }

  internal sealed class AssemblyModule
  {
    private readonly List<AssemblyInstruction> _instructions = new();

    public string Name { get; }
    public ExportAddress ExportAddress { get; }
    public IReadOnlyList<AssemblyInstruction> Instructions => _instructions;

    public AssemblyModule(string name, ExportAddress exportAddress)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
      ExportAddress = exportAddress ?? throw new ArgumentNullException(nameof(exportAddress));
    }

    public void AddInstruction(AssemblyInstruction instruction)
    {
      if (instruction == null)
        throw new ArgumentNullException(nameof(instruction));

      _instructions.Add(instruction);
    }
  }

  internal sealed class AssemblyProgram
  {
    private readonly List<AssemblyDataSlot> _dataSlots = new();
    private readonly List<AssemblyModule> _modules = new();

    public IReadOnlyList<AssemblyDataSlot> DataSlots => _dataSlots;
    public IReadOnlyList<AssemblyModule> Modules => _modules;

    public void AddDataSlot(AssemblyDataSlot slot)
    {
      if (slot == null)
        throw new ArgumentNullException(nameof(slot));

      _dataSlots.Add(slot);
    }

    public void AddModule(AssemblyModule module)
    {
      if (module == null)
        throw new ArgumentNullException(nameof(module));

      _modules.Add(module);
    }
  }
}
