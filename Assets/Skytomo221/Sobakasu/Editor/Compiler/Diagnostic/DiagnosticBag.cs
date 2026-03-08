using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Text;
using Skytomo221.Sobakasu.Compiler.Syntax;

namespace Skytomo221.Sobakasu.Compiler.Diagnostic
{
  public class DiagnosticBag
  {
    private readonly List<Diagnostic> _diagnostics = new();

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public void Report(in Diagnostic diagnostic)
        => _diagnostics.Add(diagnostic);

    public void AddRange(DiagnosticBag bag)
        => _diagnostics.AddRange(bag.Diagnostics);

    public void ReportBadCharacter(TextSpan span, char c)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK0001",
          span,
          $"Unexpected character '{c}'.",
          "その文字はこの位置では使えません。"
      ));
    }

    public void ReportUnterminatedString(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK0002",
          span,
          "Unterminated string literal.",
          "Add a closing '\"' to terminate the string."
      ));
    }

    public void ReportUnexpectedToken(
      TextSpan span,
      SyntaxKind actualKind,
      SyntaxKind expectedKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1001",
          span,
          $"Unexpected token <{actualKind}>, expected <{expectedKind}>.",
          "文法が正しくありません。"
      ));
    }

    public void ReportUnexpectedMember(TextSpan span, SyntaxKind kind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1002",
          span,
          $"Unexpected member start <{kind}>.",
          "ここではメンバー宣言が必要です。"
      ));
    }

    public void ReportUnexpectedExpression(TextSpan span, SyntaxKind kind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1003",
          span,
          $"Unexpected token <{kind}> in expression.",
          "式が必要です。"
      ));
    }

    public void ReportUnsupportedEventName(TextSpan span, string eventName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2001",
          span,
          $"Unsupported event '{eventName}'.",
          "現在は on Interact() のみ対応しています。"
      ));
    }

    public void ReportUndefinedName(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2002",
          span,
          $"Undefined name '{name}'.",
          "識別子名を確認してください。"
      ));
    }

    public void ReportUndefinedMember(TextSpan span, string receiverType, string memberName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2003",
          span,
          $"'{receiverType}' does not contain a member named '{memberName}'.",
          "メンバー名を確認してください。"
      ));
    }

    public void ReportInvalidArgumentCount(TextSpan span, string callableName, int expected, int actual)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2004",
          span,
          $"'{callableName}' expects {expected} argument(s), but got {actual}.",
          "引数の数を確認してください。"
      ));
    }

    public void ReportTypeMismatch(TextSpan span, string expectedType, string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2005",
          span,
          $"Cannot convert type '{actualType}' to '{expectedType}'.",
          "引数の型を確認してください。"
      ));
    }

    public void ReportUnsupportedStatement(TextSpan span, string statementKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2006",
          span,
          $"Unsupported statement '{statementKind}'.",
          "現在は式文のみ対応しています。"
      ));
    }

    public void ReportUnsupportedExpression(TextSpan span, string expressionKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2007",
          span,
          $"Unsupported expression '{expressionKind}'.",
          "現在の対応範囲外の式です。"
      ));
    }

    public void ReportUnsupportedCallTarget(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2008",
          span,
          "Only member call expressions are supported as call targets.",
          "現在は Debug.Log(...) の呼び出しのみ対応しています。"
      ));
    }

    public void ReportUnsupportedMember(TextSpan span, string memberText)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2009",
          span,
          $"Unsupported top-level member '{memberText}'.",
          "現在は on Interact() のみ対応しています。"
      ));
    }

    public void ReportLoweringError(string message)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK3001",
          new TextSpan(0, 0),
          message,
          "IrLowerer の入力または変換ルールを確認してください。"
      ));
    }

    public void ReportAssemblerError(string message)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK5001",
          new TextSpan(0, 0),
          message,
          "UasmAssembler の命令変換ルールを確認してください。"
      ));
    }
  }
}
