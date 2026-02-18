using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{

  internal static class SpanUtil
  {
    public static TextSpan Combine(TextSpan a, TextSpan b)
    {
      int start = a.Start;
      int end = Math.Max(a.End, b.End);
      int length = Math.Max(0, end - start);
      return new TextSpan(start, length, a.Line, a.Column);
    }
  }
}
