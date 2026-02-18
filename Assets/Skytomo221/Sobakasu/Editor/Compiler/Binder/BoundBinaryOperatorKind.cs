using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{

  public enum BoundBinaryOperatorKind
  {
    Add, Subtract, Multiply, Divide,
    Equals, NotEquals,
    Less, LessOrEqual, Greater, GreaterOrEqual,
    LogicalAnd, LogicalOr
  }
}
