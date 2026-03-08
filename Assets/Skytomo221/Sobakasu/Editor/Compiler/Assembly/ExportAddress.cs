using System;

namespace Skytomo221.Sobakasu.Compiler.Assembly
{
  internal sealed class ExportAddress
  {
    public string ExportName { get; }
    public string Label { get; }

    public ExportAddress(string exportName, string label)
    {
      ExportName = exportName ?? throw new ArgumentNullException(nameof(exportName));
      Label = label ?? throw new ArgumentNullException(nameof(label));
    }
  }
}
