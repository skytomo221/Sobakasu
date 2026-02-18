using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public sealed class BoundLiteralExpression : BoundExpression
  {
    public object? Value { get; }
    public BoundLiteralExpression(object? value, TypeSymbol type) : base(type) => Value = value;
  }
}
