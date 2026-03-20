using System;

namespace Skytomo221.Sobakasu.Compiler.Assembly
{
  internal sealed class JumpLabel
  {
    public string Name { get; }

    public JumpLabel(string name)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
    }
  }
}
