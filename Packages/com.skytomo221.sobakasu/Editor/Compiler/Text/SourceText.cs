#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace Skytomo221.Sobakasu.Compiler.Text
{
  public sealed class SourceText
  {
    private readonly TextLine[] _lines;

    public string Text { get; }

    public int Length => Text.Length;

    public IReadOnlyList<TextLine> Lines => _lines;

    public char this[int index] => (uint)index < (uint)Text.Length ? Text[index] : '\0';

    public SourceText(string text)
    {
      Text = text ?? throw new ArgumentNullException(nameof(text));
      _lines = ParseLines(Text);
    }

    public static SourceText From(string text) => new SourceText(text);

    public static SourceText FromFile(string path)
    {
      if (path is null) throw new ArgumentNullException(nameof(path));
      return new SourceText(File.ReadAllText(path));
    }

    public string ToString(TextSpan span)
    {
      // TextSpan: Start/Length 前提
      if (span.Length == 0) return string.Empty;
      if (span.Start < 0) return string.Empty;
      if (span.Start >= Text.Length) return string.Empty;

      int maxLen = Text.Length - span.Start;
      int len = span.Length <= maxLen ? span.Length : maxLen;
      if (len <= 0) return string.Empty;

      return Text.Substring(span.Start, len);
    }

    public TextLine GetLineFromPosition(int position)
    {
      // position は "文字インデックス"。終端(Length)は許容し、最後の行に属すると扱う。
      if (position < 0) position = 0;
      if (position > Length) position = Length;

      // 空ファイルでも最低1行作る実装なのでここで落ちない
      int lo = 0;
      int hi = _lines.Length - 1;

      while (lo <= hi)
      {
        int mid = lo + ((hi - lo) / 2);
        var line = _lines[mid];

        if (position < line.Start)
        {
          hi = mid - 1;
          continue;
        }

        // 行の所属判定は "改行込み終端" を使うと自然（\r\n の場合も含められる）
        if (position >= line.EndIncludingLineBreak)
        {
          lo = mid + 1;
          continue;
        }

        return line;
      }

      // position == Length などの境界は最後の行へ
      return _lines[_lines.Length - 1];
    }

    private static TextLine[] ParseLines(string text)
    {
      var lines = new List<TextLine>(capacity: 64);

      int position = 0;
      int lineStart = 0;

      while (position < text.Length)
      {
        int lineBreakWidth = GetLineBreakWidth(text, position);
        if (lineBreakWidth == 0)
        {
          position++;
          continue;
        }

        int lineLength = position - lineStart;
        int lineLengthIncludingLineBreak = lineLength + lineBreakWidth;
        lines.Add(new TextLine(lineStart, lineLength, lineLengthIncludingLineBreak));

        position += lineBreakWidth;
        lineStart = position;
      }

      // 最終行（末尾に改行があってもなくても、常に1行を持つ）
      int lastLength = text.Length - lineStart;
      lines.Add(new TextLine(lineStart, lastLength, lastLength));

      return lines.ToArray();
    }

    private static int GetLineBreakWidth(string text, int position)
    {
      char c = text[position];

      // Windows: \r\n
      if (c == '\r')
      {
        if (position + 1 < text.Length && text[position + 1] == '\n')
          return 2;
        return 1; // old Mac: \r
      }

      // Unix: \n
      if (c == '\n')
        return 1;

      return 0;
    }
  }
}