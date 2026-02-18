namespace Skytomo221.Sobakasu.Compiler.Lexer
{
  public readonly struct TextSpan
  {
    public readonly int Start;
    public readonly int Length;
    public readonly int Line;   // 1-based
    public readonly int Column; // 1-based

    public int End => Start + Length;

    public TextSpan(int start, int length, int line, int column)
    {
      Start = start;
      Length = length;
      Line = line;
      Column = column;
    }

    public override string ToString() => $"(Ln {Line}, Col {Column}, Start {Start}, Len {Length})";
  }
}
