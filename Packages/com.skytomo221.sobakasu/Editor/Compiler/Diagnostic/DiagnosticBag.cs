using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

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
          "Remove the character or replace it with supported syntax."
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

    public void ReportInvalidEscapeSequence(TextSpan span, string escapeText)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK0003",
          span,
          $"Invalid escape sequence '{escapeText}'.",
          "Use a supported escape sequence for the current literal kind."
      ));
    }

    public void ReportInvalidNumericLiteral(TextSpan span, string literalText)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK0004",
          span,
          $"Invalid numeric literal '{literalText}'.",
          "Check the base prefix, suffix, underscore placement, and numeric range."
      ));
    }

    public void ReportUnterminatedCharacterLiteral(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK0005",
          span,
          "Unterminated character literal.",
          "Add a closing '\\'' to terminate the character literal."
      ));
    }

    public void ReportMalformedCharacterLiteral(TextSpan span, string literalText)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK0006",
          span,
          $"Malformed character literal '{literalText}'.",
          "Use exactly one UTF-16 code unit or a supported escape sequence inside single quotes."
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
          "Fix the token order so the parser can continue."
      ));
    }

    public void ReportUnexpectedMember(TextSpan span, SyntaxKind kind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1002",
          span,
          $"Unexpected member start <{kind}>.",
          "Only supported top-level declarations can appear here."
      ));
    }

    public void ReportUnexpectedExpression(TextSpan span, SyntaxKind kind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1003",
          span,
          $"Unexpected token <{kind}> in expression.",
          "Replace it with a valid expression."
      ));
    }

    public void ReportInvalidUseDirective(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1004",
          span,
          "Invalid use directive.",
          "Use 'use <path> [as <alias>];' with a dotted identifier path."
      ));
    }

    public void ReportUnsupportedEventName(TextSpan span, string eventName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2001",
          span,
          $"Unsupported event '{eventName}'.",
          "Only on Interact() is supported right now."
      ));
    }

    public void ReportUndefinedName(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2002",
          span,
          $"Undefined name '{name}'.",
          "Declare the symbol before using it."
      ));
    }

    public void ReportUndefinedMember(TextSpan span, string receiverType, string memberName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2003",
          span,
          $"'{receiverType}' does not contain a member named '{memberName}'.",
          "Use a supported member for the receiver type."
      ));
    }

    public void ReportInvalidArgumentCount(TextSpan span, string callableName, int expected, int actual)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2004",
          span,
          $"'{callableName}' expects {expected} argument(s), but got {actual}.",
          "Adjust the argument count to match the callable signature."
      ));
    }

    public void ReportTypeMismatch(TextSpan span, string expectedType, string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2005",
          span,
          $"Cannot convert type '{actualType}' to '{expectedType}'.",
          "Make the expression type match the expected type."
      ));
    }

    public void ReportUnsupportedStatement(TextSpan span, string statementKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2006",
          span,
          $"Unsupported statement '{statementKind}'.",
          "Use a statement form that the compiler currently supports."
      ));
    }

    public void ReportUnsupportedExpression(TextSpan span, string expressionKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2007",
          span,
          $"Unsupported expression '{expressionKind}'.",
          "Use an expression form that the compiler currently supports."
      ));
    }

    public void ReportUnsupportedCallTarget(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2008",
          span,
          "Only member call expressions are supported as call targets.",
          "Use a supported member call such as Debug.Log(...)."
      ));
    }

    public void ReportUnsupportedMember(TextSpan span, string memberText)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2009",
          span,
          $"Unsupported top-level member '{memberText}'.",
          "Only supported top-level members can appear here."
      ));
    }

    public void ReportCannotInferArrayType(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2010",
          span,
          "Cannot infer the element type of this array literal.",
          "Use at least one non-null element so the array element type can be inferred."
      ));
    }

    public void ReportArrayElementTypeMismatch(TextSpan span, string expectedType, string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2011",
          span,
          $"Array literal element type '{actualType}' does not match '{expectedType}'.",
          "All array literal elements must share a single element type."
      ));
    }

    public void ReportCallTargetIsNotMethod(TextSpan span, string targetName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2012",
          span,
          $"'{targetName}' is not a method.",
          "Call a resolved method symbol instead of a non-callable expression."
      ));
    }

    public void ReportNoMatchingOverload(TextSpan span, string callableName, string argumentTypes)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2013",
          span,
          $"No overload of '{callableName}' matches argument type(s): {argumentTypes}.",
          "Adjust the argument types so they match one of the available overloads."
      ));
    }

    public void ReportMissingVariableInitializer(TextSpan span, string variableName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2014",
          span,
          $"Local variable '{variableName}' requires an initializer.",
          "Add '= <expr>' to the declaration."
      ));
    }

    public void ReportUnknownType(TextSpan span, string typeName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2015",
          span,
          $"Unknown type '{typeName}'.",
          "Use a supported built-in type name."
      ));
    }

    public void ReportCannotAssignToImmutableLocal(TextSpan span, string variableName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2016",
          span,
          $"Cannot assign to immutable local '{variableName}'.",
          "Add 'mut' to the declaration if reassignment is required."
      ));
    }

    public void ReportInvalidAssignmentTarget(TextSpan span, string targetName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2017",
          span,
          $"'{targetName}' is not an assignable local variable.",
          "Assign only to a previously declared local variable."
      ));
    }

    public void ReportCannotInferVariableType(TextSpan span, string variableName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2018",
          span,
          $"Cannot infer the type of local variable '{variableName}'.",
          "Provide a concrete initializer type or add an explicit type annotation."
      ));
    }

    public void ReportUnresolvedUsePath(TextSpan span, string importedPath)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2019",
          span,
          $"Could not resolve use path '{importedPath}'.",
          "Import a supported namespace, type, or static method group."
      ));
    }

    public void ReportImportConflict(
        TextSpan span,
        string introducedName,
        string existingTarget,
        string newTarget)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2020",
          span,
          $"Import name '{introducedName}' conflicts between '{existingTarget}' and '{newTarget}'.",
          "Rename one import with 'as' or remove the conflicting import."
      ));
    }

    public void ReportAmbiguousImportedReference(
        TextSpan span,
        string referenceName,
        string candidates)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2021",
          span,
          $"Imported reference '{referenceName}' is ambiguous. Candidates: {candidates}.",
          "Use a more specific path or remove the conflicting imports."
      ));
    }

    public void ReportNoCallableExternCandidate(TextSpan span, string callableName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2022",
          span,
          $"No callable extern candidates were found for '{callableName}'.",
          "Import or call a method group that contains at least one callable Udon extern."
      ));
    }

    public void ReportAmbiguousExternOverload(
        TextSpan span,
        string callableName,
        string candidates)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2023",
          span,
          $"Call to '{callableName}' is ambiguous between overloads: {candidates}.",
          "Adjust the argument types or import a less ambiguous callable."
      ));
    }

    public void ReportExternCandidatesNotUdonCallable(
        TextSpan span,
        string callableName,
        string details)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2024",
          span,
          $"Extern candidates were discovered for '{callableName}', but none are callable as Udon externs. {details}",
          "Use a Udon-exposed API surface or change the import/call target."
      ));
    }

    public void ReportUnsupportedUseTarget(
        TextSpan span,
        string importedPath,
        string reason)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2025",
          span,
          $"Unsupported use target '{importedPath}'. {reason}",
          "Import only namespaces, types, or static method groups supported by v1."
      ));
    }

    public void ReportLoweringError(string message)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK3001",
          new TextSpan(0, 0),
          message,
          "Fix the lowering issue before generating UASM."
      ));
    }

    public void ReportAssemblerError(string message)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK5001",
          new TextSpan(0, 0),
          message,
          "Fix the assembler issue before using the generated UASM."
      ));
    }
  }
}
