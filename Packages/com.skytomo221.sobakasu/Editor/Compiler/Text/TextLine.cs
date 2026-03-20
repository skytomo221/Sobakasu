namespace Skytomo221.Sobakasu.Compiler.Text
{
  public class TextLine
  {
    public int Start { get; }

    public int Length { get; }

    public int LengthIncludingLineBreak { get; }

    public int End => Start + Length;

    public int EndIncludingLineBreak => Start + LengthIncludingLineBreak;

    public TextLine(int start, int length, int lengthIncludingLineBreak)
    {
      Start = start;
      Length = length;
      LengthIncludingLineBreak = lengthIncludingLineBreak;
    }
  }
}