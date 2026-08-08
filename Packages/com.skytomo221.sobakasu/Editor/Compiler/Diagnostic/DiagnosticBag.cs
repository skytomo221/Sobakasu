using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Diagnostic
{
  public class DiagnosticBag
  {
    private readonly List<Diagnostic> _diagnostics = new();

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public string SourcePath { get; set; } = string.Empty;

    public bool HasErrors
    {
      get
      {
        foreach (var diagnostic in _diagnostics)
        {
          if (diagnostic.Severity == DiagnosticSeverity.Error)
            return true;
        }

        return false;
      }
    }

    public void Report(in Diagnostic diagnostic)
    {
      if (string.IsNullOrEmpty(diagnostic.SourcePath) &&
          !string.IsNullOrEmpty(SourcePath))
      {
        _diagnostics.Add(new Diagnostic(
            diagnostic.Severity,
            diagnostic.Code,
            diagnostic.Span,
            diagnostic.Message,
            diagnostic.Hint,
            SourcePath));
        return;
      }

      _diagnostics.Add(diagnostic);
    }

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

    public void ReportDoubleColonModulePath(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1024",
          span,
          "'::' is not supported in Sobakasu module paths.",
          "Separate Sobakasu module path segments with '.'."
      ));
    }

    public void ReportInvalidModDeclaration(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1025",
          span,
          "Invalid mod declaration.",
          "Use 'mod <child>;' or 'pub mod <child>;' with one child module name."
      ));
    }

    public void ReportUnsupportedPatternForm(TextSpan span, string patternText)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1027",
          span,
          $"Unsupported pattern form '{patternText}'.",
          "Use '_', a supported literal, or a qualified enum variant pattern."
      ));
    }

    public void ReportModMustBeTopLevel(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1026",
          span,
          "mod declarations are only allowed at the top level.",
          "Move the mod declaration outside the function or block."
      ));
    }

    public void ReportMissingLoopLabelColon(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1005",
          span,
          "Loop label declaration is missing ':'.",
          "Write the label as \"'label: while ...\" or \"'label: loop ...\"."
      ));
    }

    public void ReportInvalidLoopLabelTarget(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1006",
          span,
          "A loop label can only be attached to 'while' or 'loop'.",
          "Place the label immediately before a while or loop expression."
      ));
    }

    public void ReportControlBodyRequiresBlock(TextSpan span, string keyword)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1007",
          span,
          $"The body following '{keyword}' must be enclosed in braces.",
          $"Write '{keyword} ... {{ ... }}'; single-statement bodies cannot omit braces."
      ));
    }

    public void ReportJumpDoesNotAcceptValue(TextSpan span, string keyword)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1008",
          span,
          $"'{keyword}' does not accept a value.",
          $"Write '{keyword};' or '{keyword} 'label;'."
      ));
    }

    public void ReportInvalidJumpSyntax(TextSpan span, string keyword)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1009",
          span,
          $"Invalid token sequence in '{keyword}' statement.",
          "End the jump statement with ';' and place an optional label before any break value."
      ));
    }

    public void ReportUnknownSynchronizationMode(TextSpan span, string mode)
    {
      var displayMode = string.IsNullOrEmpty(mode) ? "<empty>" : mode;
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1010",
          span,
          $"Unknown synchronization mode '{displayMode}'.",
          "Use one of the allowed modes: none, linear, smooth."
      ));
    }

    public void ReportSynchronizationModeArgumentCount(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1011",
          span,
          "A synchronization modifier accepts exactly one mode.",
          "Write sync(none), sync(linear), or sync(smooth)."
      ));
    }

    public void ReportStateModifierOrder(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1012",
          span,
          "State declaration modifiers are in the wrong position.",
          "Use the canonical order: pub, sync(...), let, mut, name."
      ));
    }

    public void ReportDuplicateStateModifier(TextSpan span, string modifier)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1013",
          span,
          $"State declaration modifier '{modifier}' is duplicated.",
          $"Remove the duplicate '{modifier}' modifier."
      ));
    }

    public void ReportPublicModifierOnlyOnTopLevelState(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1014",
          span,
          "pub can only be used on top-level state declarations.",
          "Move the declaration to the top level or remove 'pub'."
      ));
    }

    public void ReportSynchronizedStateMustBeTopLevel(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1015",
          span,
          "Synchronized state must be declared at top level.",
          "Move the declaration to the top level and write 'sync let mut name = value;'."
      ));
    }

    public void ReportUnsupportedTopLevelModifier(
        TextSpan span,
        string modifier,
        SyntaxKind declarationKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1016",
          span,
          $"Modifier '{modifier}' is not supported on <{declarationKind}> declarations.",
          "Use sync only on top-level state; use pub only on supported state, function, external binding, or impl-method declarations."
      ));
    }

    public void ReportMissingTopLevelStateInitializer(TextSpan span, string stateName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1017",
          span,
          $"Top-level state '{stateName}' requires an initializer.",
          "Add '= <compile-time constant>' before the terminating semicolon."
      ));
    }

    public void ReportQuestionMarkNotAllowedInName(TextSpan span, string nameKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1018",
          span,
          $"'?' can only be used at the end of a callable name; it is not allowed in a {nameKind} name.",
          "Remove '?' or use it once at the end of a function name."
      ));
    }

    public void ReportMultipleCallableQuestionMarks(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1019",
          span,
          "A callable name can end with at most one '?'.",
          "Remove the extra '?' suffix."
      ));
    }

    public void ReportQuestionMarkMustEndCallableName(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1022",
          span,
          "'?' can only be used at the end of a callable name.",
          "Move '?' to the end of the name or remove it."
      ));
    }

    public void ReportBangCallableNameSuffix(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1020",
          span,
          "'!' cannot be used as a callable-name suffix.",
          "'!' is reserved for future macro syntax."
      ));
    }

    public void ReportCallableParametersRequireParentheses(
        TextSpan span,
        string declarationKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1021",
          span,
          $"Parameters in a {declarationKind} declaration must be enclosed in parentheses.",
          "Use '(name: Type)' for one or more parameters; only an empty parameter list may omit parentheses."
      ));
    }

    public void ReportUnexpectedImplMember(TextSpan span, SyntaxKind actualKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK1023",
          span,
          $"Unexpected token '{actualKind}' in impl block.",
          "Only fn and static fn declarations are allowed in an impl block."
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
          "Add an explicit array type, such as 'let values: [i32] = [];', or provide a non-null element."
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

    public void ReportUnsupportedUnaryOperator(
        TextSpan span,
        string operatorText,
        string operandType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2026",
          span,
          $"Operator '{operatorText}' is not defined for operand type '{operandType}'.",
          "Use a supported unary operator for the operand type."
      ));
    }

    public void ReportUnsupportedBinaryOperator(
        TextSpan span,
        string operatorText,
        string leftType,
        string rightType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2027",
          span,
          $"Operator '{operatorText}' is not defined for operand types '{leftType}' and '{rightType}'.",
          "Use a supported binary operator with exact operand types."
      ));
    }

    public void ReportAmbiguousOperator(
        TextSpan span,
        string operatorText,
        string operandTypes,
        string candidates)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2028",
          span,
          $"Operator '{operatorText}' for operand type(s) {operandTypes} is ambiguous. Candidates: {candidates}.",
          "Make the operand types unambiguous or use a different operator."
      ));
    }

    public void ReportInvalidCompoundAssignmentTarget(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2029",
          span,
          "Compound assignment requires a mutable local variable target in v1.",
          "Use a mutable local variable on the left-hand side."
      ));
    }

    public void ReportShortCircuitRequiresBoolOperands(
        TextSpan span,
        string operatorText,
        string leftType,
        string rightType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2030",
          span,
          $"Operator '{operatorText}' requires bool operands, but got '{leftType}' and '{rightType}'.",
          "Use bool expressions on both sides of the short-circuit operator."
      ));
    }

    public void ReportUnknownEvent(TextSpan span, string eventName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2031",
          span,
          $"Unknown event '{eventName}'.",
          "Use an event name listed in the Sobakasu event catalog."
      ));
    }

    public void ReportDuplicateEvent(TextSpan span, string eventName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2032",
          span,
          $"Event '{eventName}' is already declared in this file.",
          "Declare each event at most once."
      ));
    }

    public void ReportUnsupportedEventSignature(TextSpan span, string eventName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2033",
          span,
          $"Event '{eventName}' is known but its signature is not supported yet.",
          "Wait for this Unity event signature to be confirmed before using it."
      ));
    }

    public void ReportEventParameterCountMismatch(
        TextSpan span,
        string eventName,
        int expected,
        int actual)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2034",
          span,
          $"Event '{eventName}' expects {expected} parameter(s), but got {actual}.",
          "Match the event parameter count defined by the event catalog."
      ));
    }

    public void ReportEventParameterTypeMismatch(
        TextSpan span,
        string eventName,
        int parameterIndex,
        string expectedType,
        string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2035",
          span,
          $"Event '{eventName}' parameter {parameterIndex + 1} must be '{expectedType}', but got '{actualType}'.",
          "Use the exact parameter type required by the event catalog."
      ));
    }

    public void ReportEventReturnTypeRequired(
        TextSpan span,
        string eventName,
        string returnType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2036",
          span,
          $"Event '{eventName}' must declare return type '{returnType}'.",
          "Add an explicit return type annotation to this event declaration."
      ));
    }

    public void ReportEventReturnTypeMismatch(
        TextSpan span,
        string eventName,
        string expectedType,
        string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2037",
          span,
          $"Event '{eventName}' must return '{expectedType}', but declares '{actualType}'.",
          "Make the event return annotation match the event catalog."
      ));
    }

    public void ReportReturnValueRequired(TextSpan span, string eventName, string returnType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2038",
          span,
          $"Declaration '{eventName}' must return a value of type '{returnType}'.",
          "Add a return statement with a value."
      ));
    }

    public void ReportReturnValueNotAllowed(TextSpan span, string eventName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2039",
          span,
          $"Declaration '{eventName}' does not return a value.",
          "Use 'return;' or remove the returned expression."
      ));
    }

    public void ReportReturnTypeMismatch(
        TextSpan span,
        string expectedType,
        string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2040",
          span,
          $"Return expression type '{actualType}' does not match '{expectedType}'.",
          "Return an expression with the declared return type."
      ));
    }

    public void ReportDuplicateParameterName(TextSpan span, string parameterName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2041",
          span,
          $"Parameter '{parameterName}' is already declared for this declaration.",
          "Use a unique parameter name."
      ));
    }

    public void ReportEventRequiresComponent(
        TextSpan span,
        string eventName,
        string requirement)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Warning,
          "SBK2042",
          span,
          $"Event '{eventName}' requires component '{requirement}'.",
          "Ensure the corresponding component is present on the UdonBehaviour GameObject."
      ));
    }

    public void ReportDuplicateFunctionName(TextSpan span, string functionName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2043",
          span,
          $"Function '{functionName}' is already declared in this file.",
          "Declare each user-defined function at most once."
      ));
    }

    public void ReportAmbiguousUserFunctionExternCall(TextSpan span, string functionName, string externCandidate)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2044",
          span,
          $"Call to '{functionName}' is ambiguous between a user-defined function and extern '{externCandidate}'.",
          "Rename the function or the import alias so the call target is unambiguous."
      ));
    }

    public void ReportRecursiveFunction(TextSpan span, string functionName, string cycle)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2045",
          span,
          $"Function '{functionName}' is recursive in v1. Cycle: {cycle}.",
          "Rewrite the function without recursion or wait for runtime call-frame support."
      ));
    }

    public void ReportFirstClassFunctionValueNotSupported(TextSpan span, string functionName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2046",
          span,
          $"Function '{functionName}' cannot be used as a value in v1.",
          "Call the function directly instead of storing or passing it as a value."
      ));
    }

    public void ReportConditionRequiresBool(
        TextSpan span,
        string constructName,
        string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2047",
          span,
          $"The '{constructName}' condition must have type 'bool', but got '{actualType}'.",
          "Use a bool expression; Sobakasu does not apply truthy/falsy conversion."
      ));
    }

    public void ReportIfValueRequiresElse(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2048",
          span,
          "An if expression without else cannot produce a value.",
          "Add an else branch with the same result type, or make the then branch return u0."
      ));
    }

    public void ReportIfBranchTypeMismatch(
        TextSpan span,
        string thenType,
        string elseType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2049",
          span,
          $"If branch types do not match: then is '{thenType}', else is '{elseType}'.",
          "Return exactly the same type from every reachable branch."
      ));
    }

    public void ReportBreakValueTargetsWhile(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2050",
          span,
          "A value-producing break cannot target a while expression.",
          "Use 'break;' for while, or target a loop expression when returning a value."
      ));
    }

    public void ReportMixedLoopBreakValues(TextSpan span, string label)
    {
      var target = string.IsNullOrEmpty(label)
          ? "this loop"
          : $"loop '{label}";
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2051",
          span,
          $"Value-less and value-producing break statements are mixed for {target}.",
          "Use either 'break;' everywhere or give every reachable break a value."
      ));
    }

    public void ReportLoopBreakTypeMismatch(
        TextSpan span,
        string expectedType,
        string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2052",
          span,
          $"Loop break value type '{actualType}' does not match '{expectedType}'.",
          "Use exactly the same value type for every break targeting this loop."
      ));
    }

    public void ReportJumpOutsideLoop(TextSpan span, string statementName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2053",
          span,
          $"'{statementName}' can only be used inside a while or loop expression.",
          "Move the statement into a loop or specify a lexically enclosing loop label."
      ));
    }

    public void ReportUnknownLoopLabel(TextSpan span, string label)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2054",
          span,
          $"Unknown or non-enclosing loop label '{label}'.",
          "Target a label declared on a lexically enclosing while or loop expression."
      ));
    }

    public void ReportDuplicateLoopLabel(TextSpan span, string label)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2055",
          span,
          $"Loop label '{label}' overlaps an active label with the same name.",
          "Rename one label so all simultaneously active loop labels are unique."
      ));
    }

    public void ReportMissingStateInitializer(TextSpan span, string stateName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2056",
          span,
          $"Top-level state '{stateName}' requires an initializer.",
          "Add '= <compile-time constant>' to the declaration."
      ));
    }

    public void ReportCannotInferStateType(TextSpan span, string stateName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2057",
          span,
          $"Cannot infer the type of top-level state '{stateName}'.",
          "Add an explicit type annotation with a compatible constant initializer."
      ));
    }

    public void ReportDuplicateState(TextSpan span, string stateName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2058",
          span,
          $"Top-level state '{stateName}' is already declared in this file.",
          "Declare each top-level state name at most once."
      ));
    }

    public void ReportCannotAssignToImmutableState(TextSpan span, string stateName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2059",
          span,
          $"Cannot assign to immutable state '{stateName}'.",
          "Add 'mut' to the top-level state declaration if reassignment is required."
      ));
    }

    public void ReportSynchronizedStateMustBeMutable(TextSpan span, string stateName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2060",
          span,
          $"Synchronized state binding '{stateName}' must be mutable.",
          $"Write 'sync let mut {stateName} = <value>;'."
      ));
    }

    public void ReportUnsupportedStateSynchronization(
        TextSpan span,
        string stateName,
        string mode,
        string typeName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2061",
          span,
          $"State '{stateName}' of type '{typeName}' is not supported for {mode} synchronization.",
          "Choose a synchronization mode supported by the SDK for this type."
      ));
    }

    public void ReportStateInitializerMustBeConstant(TextSpan span, string stateName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2062",
          span,
          $"Top-level initializer for state '{stateName}' must be a compile-time constant.",
          "Use a literal, null for a reference type, or a supported unary constant expression."
      ));
    }

    public void ReportStateNameConflict(TextSpan span, string stateName, string otherKind)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2063",
          span,
          $"Top-level state '{stateName}' conflicts with a {otherKind} of the same name.",
          "Rename one of the top-level declarations."
      ));
    }

    public void ReportCallableRequiresArguments(
        TextSpan span,
        string callableName,
        int requiredArgumentCount)
    {
      var countText = requiredArgumentCount < 0
          ? "one or more arguments"
          : $"{requiredArgumentCount} argument(s)";
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2064",
          span,
          $"Callable '{callableName}' requires {countText}; parentheses can only be omitted for a zero-argument call.",
          $"Call it as '{callableName}(...)'."
      ));
    }

    public void ReportUnknownImplTarget(TextSpan span, string typeName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2065",
          span,
          $"Unknown impl target type '{typeName}'.",
          "Declare or import the Sobakasu type before adding an impl block."
      ));
    }

    public void ReportUnknownExternalType(TextSpan span, string typeName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2066",
          span,
          $"Unknown external type '{typeName}'.",
          "Use a fully-qualified CLR type name available to the Unity Editor."
      ));
    }

    public void ReportExternalTypeNotExposed(TextSpan span, string typeName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2067",
          span,
          $"External type '{typeName}' is not exposed to Udon.",
          "Bind only runtime types supported by the installed VRChat SDK."
      ));
    }

    public void ReportDuplicateExternalTypeBinding(TextSpan span, string typeName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2068",
          span,
          $"Duplicate external type binding for Sobakasu type '{typeName}'.",
          "Keep exactly one external binding declaration for this type."
      ));
    }

    public void ReportExternalRuntimeTypeAlreadyBound(
        TextSpan span,
        string runtimeType,
        string existingType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2069",
          span,
          $"External runtime type '{runtimeType}' is already bound as '{existingType}'.",
          "Reuse the existing Sobakasu type instead of creating another binding."
      ));
    }

    public void ReportCannotExternallyBindBuiltInType(TextSpan span, string typeName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2070",
          span,
          $"Built-in type '{typeName}' cannot be externally bound.",
          "Use a normal impl block to add methods to a built-in type."
      ));
    }

    public void ReportDuplicateMethodSignature(TextSpan span, string methodName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2071",
          span,
          $"Duplicate method signature for '{methodName}'.",
          "Change the method name or one of its explicit parameter types."
      ));
    }

    public void ReportExplicitSelfParameter(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2072",
          span,
          "An explicit self parameter is not allowed.",
          "Instance methods receive self: Self implicitly inside impl blocks."
      ));
    }

    public void ReportSelfUnavailableInStaticFunction(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2073",
          span,
          "self is unavailable in a static function.",
          "Remove static or pass the value as an explicit parameter."
      ));
    }

    public void ReportSelfTypeOutsideImpl(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2074",
          span,
          "Self is only available inside an impl block.",
          "Use a concrete type name outside impl."
      ));
    }

    public void ReportInvalidOperatorName(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2075",
          span,
          $"Invalid operator declaration '{name}'.",
          "Declare operators only as instance functions inside impl."
      ));
    }

    public void ReportOperatorCannotBeOverloaded(TextSpan span, string operatorText)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2076",
          span,
          $"Operator '{operatorText}' cannot be overloaded.",
          "Short-circuit, assignment, and compound-assignment operators are compiler-defined."
      ));
    }

    public void ReportInvalidUnaryOperatorArity(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2077",
          span,
          $"Unary operator '{name}' must not have explicit parameters.",
          "The operand is supplied through the implicit self receiver."
      ));
    }

    public void ReportInvalidBinaryOperatorArity(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2078",
          span,
          $"Binary operator '{name}' must have exactly one explicit parameter.",
          "The left operand is supplied through the implicit self receiver."
      ));
    }

    public void ReportComparisonOperatorMustReturnBool(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2079",
          span,
          $"Comparison operator '{name}' must return bool.",
          "Change the declared return type to bool."
      ));
    }

    public void ReportBuiltInOperatorCannotBeRedefined(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2080",
          span,
          $"Built-in operator signature '{name}' cannot be redefined.",
          "Add only operator signatures that do not conflict with compiler built-ins."
      ));
    }

    public void ReportNoApplicableMethodOverload(
        TextSpan span,
        string methodName,
        string argumentTypes)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2081",
          span,
          $"No applicable method overload for '{methodName}' with argument types {argumentTypes}.",
          "Check the argument count and types."
      ));
    }

    public void ReportAmbiguousMethodOverload(
        TextSpan span,
        string methodName,
        string candidates)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2082",
          span,
          $"Ambiguous method overload for '{methodName}'. Candidates: {candidates}.",
          "Use argument types that select one overload exactly."
      ));
    }

    public void ReportUnknownExternalMember(
        TextSpan span,
        string typeName,
        string memberName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2083",
          span,
          $"Unknown external member '{typeName}.{memberName}'.",
          "Check the runtime member name and the installed SDK version."
      ));
    }

    public void ReportExternalMemberNotExposed(
        TextSpan span,
        string memberName,
        string details)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2084",
          span,
          $"External member '{memberName}' is not exposed to Udon. {details}",
          "Use an API exposed by the installed VRChat SDK."
      ));
    }

    public void ReportNoApplicableExternalOverload(
        TextSpan span,
        string memberName,
        string argumentTypes)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2085",
          span,
          $"No applicable external overload for '{memberName}' with argument types {argumentTypes}.",
          "Check the external member signature and argument types."
      ));
    }

    public void ReportAmbiguousExternalOverload(
        TextSpan span,
        string memberName,
        string candidates)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2086",
          span,
          $"Ambiguous external overload for '{memberName}'. Candidates: {candidates}.",
          "Use argument types that select one Udon extern signature."
      ));
    }

    public void ReportUnsupportedExternalExpression(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2087",
          span,
          "Unsupported external expression.",
          "Use extern with a method, getter, setter, constructor, or unary/binary operator access."
      ));
    }

    public void ReportPublicModifierNotAllowedOnAdditionalImpl(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2088",
          span,
          "pub is not allowed on an additional impl block.",
          "Put pub on individual methods; type visibility belongs to its external binding."
      ));
    }

    public void ReportInvalidExternalBindingTarget(TextSpan span, string typeName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2089",
          span,
          $"External binding target '{typeName}' must be a new simple Sobakasu type name.",
          "Use one identifier on the left side of '= extern'."
      ));
    }

    public void ReportLogicalModuleDoesNotExist(TextSpan span, string path)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4004",
          span,
          $"Logical module does not exist for use path '{path}'.",
          "Create the convention-based .sobakasu module below StandardLibrary~. use does not fall back to external APIs."
      ));
    }

    public void ReportDeclarationNotPublic(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4007",
          span,
          $"Declaration '{name}' is not public.",
          "Add pub to the declaration or import a public wrapper."
      ));
    }

    public void ReportDuplicateModuleAlias(TextSpan span, string alias)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4008",
          span,
          $"Duplicate module import alias '{alias}'.",
          "Choose a unique alias in this module."
      ));
    }

    public void ReportAmbiguousModuleImport(
        TextSpan span,
        string name,
        string first,
        string second)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4009",
          span,
          $"Ambiguous imported name '{name}': {first}, {second}.",
          "Use an as alias to give each imported declaration a unique name."
      ));
    }

    public void ReportLogicalDeclarationNotFound(TextSpan span, string path)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4010",
          span,
          $"Sobakasu declaration was not found for use path '{path}'.",
          "Check the declaration name and convention-based module path."
      ));
    }

    public void ReportExternalApiCannotBeImportedWithUse(TextSpan span, string path)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4011",
          span,
          $"External APIs cannot be imported with use: '{path}'.",
          "Wrap the API with extern, or import a Sobakasu library module that provides a wrapper."
      ));
    }

    public void ReportStateNotAllowedInStandardLibrary(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4012",
          span,
          "State declarations are not allowed in standard library modules.",
          "Move persistent state to the entry program."
      ));
    }

    public void ReportEventNotAllowedInStandardLibrary(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4013",
          span,
          "Event declarations are not allowed in standard library modules.",
          "Declare Udon event entry points only in the entry program."
      ));
    }

    public void ReportModuleNotPublic(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4021",
          span,
          $"Module '{name}' is private from this location.",
          "Use a public parent re-export or change the parent declaration to pub mod."
      ));
    }

    public void ReportUnsupportedObjectStateInitializer(TextSpan span, string stateName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2090",
          span,
          $"Top-level object state '{stateName}' currently supports only a null initializer.",
          "Initialize the object state with null and assign a value at runtime."
      ));
    }

    public void ReportArrayTypeNotAvailable(
        TextSpan span,
        string typeName,
        string reason)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2091",
          span,
          $"Array type '{typeName}' is not available on the installed Udon target. {reason}",
          "Use an element array ABI type exposed by the installed VRChat SDK."
      ));
    }

    public void ReportUnresolvedArrayRepeatOperand(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2092",
          span,
          "The left side of this repeat array is neither a resolvable type nor a value expression.",
          "Use '[Type; length]' for default values or '[expression; length]' for repeated evaluation."
      ));
    }

    public void ReportAmbiguousArrayRepeatOperand(TextSpan span)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2093",
          span,
          "The left side of this repeat array is ambiguous between a type and a value.",
          "Rename the value binding or use an unambiguous qualified type name."
      ));
    }

    public void ReportInvalidArrayLengthType(
        TextSpan span,
        string expectedType,
        string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2094",
          span,
          $"Array length must have type '{expectedType}', but got '{actualType}'.",
          $"Convert or rewrite the length expression as '{expectedType}'."
      ));
    }

    public void ReportNegativeArrayLength(TextSpan span, int length)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2095",
          span,
          $"Array length cannot be negative ({length}).",
          "Use a non-negative i32 length."
      ));
    }

    public void ReportIndexTargetIsNotArray(TextSpan span, string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2096",
          span,
          $"A value of type '{actualType}' cannot be indexed as an array.",
          "Use indexing only on a value whose type is '[T]'."
      ));
    }

    public void ReportInvalidArrayIndexType(
        TextSpan span,
        string expectedType,
        string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2097",
          span,
          $"Array index must have type '{expectedType}', but got '{actualType}'.",
          $"Convert or rewrite the index expression as '{expectedType}'."
      ));
    }

    public void ReportArrayElementAssignmentTypeMismatch(
        TextSpan span,
        string expectedType,
        string actualType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2098",
          span,
          $"Cannot assign value of type '{actualType}' to array element type '{expectedType}'.",
          "Make the assigned value compatible with the array element type."
      ));
    }

    public void ReportUnsupportedArrayElementCompoundAssignment(
        TextSpan span,
        string operatorText,
        string elementType,
        string valueType)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2099",
          span,
          $"Operator '{operatorText}' is not available for array element type '{elementType}' and value type '{valueType}'.",
          "Use a compound operator supported by the element and right-hand-side types."
      ));
    }

    public void ReportPublicArrayTypeNotAvailable(TextSpan span, string typeName)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK2100",
          span,
          $"Array type '{typeName}' cannot be exposed as a public Udon variable.",
          "Use an array ABI type supported by the installed SDK Inspector."
      ));
    }

    public void ReportDuplicateAggregateType(TextSpan span, string name)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2101", span,
          $"Type '{name}' is declared more than once.",
          "Use a unique name for each struct, enum, and external type binding."));
    }

    public void ReportDuplicateAggregateField(TextSpan span, string type, string field)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2102", span,
          $"Struct '{type}' declares field '{field}' more than once.",
          "Remove or rename the duplicate field."));
    }

    public void ReportDuplicateEnumVariant(TextSpan span, string type, string variant)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2103", span,
          $"Enum '{type}' declares variant '{variant}' more than once.",
          "Remove or rename the duplicate variant."));
    }

    public void ReportDuplicateEnumPayloadField(
        TextSpan span,
        string type,
        string variant,
        string field)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2104", span,
          $"Enum variant '{type}.{variant}' declares payload field '{field}' more than once.",
          "Remove or rename the duplicate payload field."));
    }

    public void ReportRecursiveAggregate(TextSpan span, string cycle)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2105", span,
          $"Recursive aggregate type is not supported: {cycle}.",
          "Remove the struct/enum dependency cycle so storage can be flattened."));
    }

    public void ReportUnknownAggregateInitializerField(
        TextSpan span,
        string target,
        string field)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2106", span,
          $"Initializer for '{target}' has no field named '{field}'.",
          "Use one of the fields declared by the aggregate type."));
    }

    public void ReportMissingAggregateInitializerField(
        TextSpan span,
        string target,
        string field)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2107", span,
          $"Initializer for '{target}' is missing required field '{field}'.",
          "Specify every aggregate field; field defaults are not supported."));
    }

    public void ReportDuplicateAggregateInitializerField(
        TextSpan span,
        string target,
        string field)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2108", span,
          $"Initializer for '{target}' specifies field '{field}' more than once.",
          "Specify each aggregate field exactly once."));
    }

    public void ReportAggregateInitializerTypeMismatch(
        TextSpan span,
        string target,
        string field,
        string expected,
        string actual)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2109", span,
          $"Field '{target}.{field}' expects '{expected}', but got '{actual}'.",
          "Use a value compatible with the declared field type."));
    }

    public void ReportStructInitializerRequiresStruct(TextSpan span, string type)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2110", span,
          $"Struct initializer syntax cannot construct '{type}'.",
          "Use a user-defined struct type or a struct enum variant."));
    }

    public void ReportUnknownEnumVariant(
        TextSpan span,
        string type,
        string variant)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2111", span,
          $"Enum '{type}' has no variant named '{variant}'.",
          "Use a variant declared by the enum."));
    }

    public void ReportEnumVariantRequiresPayload(
        TextSpan span,
        string type,
        string variant)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2112", span,
          $"Enum variant '{type}.{variant}' requires a payload.",
          "Use tuple call syntax or struct initializer syntax for this variant."));
    }

    public void ReportEnumVariantConstructionForm(
        TextSpan span,
        string type,
        string variant,
        string actualForm)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2113", span,
          $"Enum variant '{type}.{variant}' cannot be constructed with {actualForm} syntax.",
          "Use the construction syntax matching the variant declaration."));
    }

    public void ReportEnumTuplePayloadArity(
        TextSpan span,
        string type,
        string variant,
        int expected,
        int actual)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2114", span,
          $"Enum variant '{type}.{variant}' expects {expected} payload value(s), but got {actual}.",
          "Pass exactly the declared number of tuple payload values."));
    }

    public void ReportEnumTuplePayloadTypeMismatch(
        TextSpan span,
        string type,
        string variant,
        int index,
        string expected,
        string actual)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2115", span,
          $"Payload {index} of '{type}.{variant}' expects '{expected}', but got '{actual}'.",
          "Use a value compatible with the declared payload type."));
    }

    public void ReportUnsupportedAggregateLeafAbi(
        TextSpan span,
        string type,
        string path,
        string leafType)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2116", span,
          $"Aggregate '{type}' field path '{path}' has unsupported Udon leaf type '{leafType}'.",
          "Use a field type that has concrete Udon storage."));
    }

    public void ReportInvalidAggregateArrayLeafAbi(
        TextSpan span,
        string arrayType,
        string path,
        string leafType,
        string reason)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2117", span,
          $"Aggregate array '{arrayType}' field path '{path}' cannot use leaf array '{leafType}'. {reason}",
          "Use aggregate fields whose typed leaf arrays are exposed by the installed SDK."));
    }

    public void ReportUnsupportedAggregateSynchronization(
        TextSpan span,
        string type,
        string path,
        string leafType,
        string mode)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2118", span,
          $"Cannot synchronize '{type}' because field path '{path}' has unsupported type '{leafType}' for mode '{mode}'.",
          "Change the field type or remove/change the synchronization mode."));
    }

    public void ReportAggregateExternBoundary(TextSpan span, string type)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2119", span,
          $"Aggregate type '{type}' cannot cross the Udon extern boundary directly.",
          "Pass the aggregate's Udon-representable leaf values explicitly."));
    }

    public void ReportModuleNotConnected(TextSpan span, string name)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4022",
          span,
          $"Module '{name}' exists but is not connected by its parent.",
          "Add mod or pub mod for this direct child in the parent module."
      ));
    }

    public void ReportDuplicateGenericParameter(
        TextSpan span,
        string declaration,
        string parameter)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2120", span,
          $"Generic declaration '{declaration}' declares type parameter '{parameter}' more than once.",
          "Use a unique name for each type parameter."));
    }

    public void ReportWrongGenericArity(
        TextSpan span,
        string type,
        int expected,
        int actual)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2121", span,
          $"Generic type '{type}' expects {expected} type argument(s), but got {actual}.",
          "Supply exactly the declared number of concrete type arguments."));
    }

    public void ReportCannotInferGenericParameter(
        TextSpan span,
        string parameter,
        string type)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2122", span,
          $"Cannot infer type parameter '{parameter}' for '{type}'.",
          "Specify explicit type arguments or provide a payload/expected type that determines this parameter."));
    }

    public void ReportConflictingGenericInference(
        TextSpan span,
        string parameter,
        string first,
        string second)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2123", span,
          $"Type parameter '{parameter}' cannot be inferred as both '{first}' and '{second}'.",
          "Use values that infer one identical concrete type argument."));
    }

    public void ReportOpenGenericType(TextSpan span, string type)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2124", span,
          $"Open generic type '{type}' cannot be used where concrete storage is required.",
          "Resolve every type parameter before lowering or runtime storage."));
    }

    public void ReportInvalidGenericImplTarget(TextSpan span, string type)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2125", span,
          $"Generic impl target '{type}' must apply every impl type parameter exactly once to one generic aggregate definition.",
          "Use a target such as 'Box<T>' or 'Pair<T, U>' without specialization."));
    }

    public void ReportNonExhaustiveMatch(TextSpan span, string missing)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2126", span,
          $"Non-exhaustive match: {missing} is not covered.",
          "Add the missing pattern or a wildcard '_' arm."));
    }

    public void ReportUnreachableMatchArm(TextSpan span)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2127", span,
          "Unreachable match arm.",
          "Remove the arm or place it before the pattern that already covers it."));
    }

    public void ReportEnumVariantBelongsToDifferentEnum(
        TextSpan span,
        string pattern,
        string expectedEnum)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2128", span,
          $"Enum variant pattern '{pattern}' does not belong to matched enum '{expectedEnum}'.",
          "Use a variant declared by the scrutinee's enum type."));
    }

    public void ReportMatchTuplePatternArity(
        TextSpan span,
        string type,
        string variant,
        int expected,
        int actual)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2129", span,
          $"Enum pattern '{type}.{variant}' expects {expected} payload binding(s), but got {actual}.",
          "Bind or discard every tuple payload position exactly once."));
    }

    public void ReportUnknownStructVariantPatternField(
        TextSpan span,
        string pattern,
        string field)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2130", span,
          $"Enum struct pattern '{pattern}' has no payload field named '{field}'.",
          "Use a field declared by the enum variant."));
    }

    public void ReportDuplicateStructVariantPatternField(
        TextSpan span,
        string pattern,
        string field)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2131", span,
          $"Enum struct pattern '{pattern}' specifies field '{field}' more than once.",
          "Specify each payload field exactly once."));
    }

    public void ReportMissingStructVariantPatternField(
        TextSpan span,
        string pattern,
        string field)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2132", span,
          $"Enum struct pattern '{pattern}' is missing payload field '{field}'.",
          "Specify every payload field; rest patterns are not supported."));
    }

    public void ReportLiteralPatternTypeMismatch(
        TextSpan span,
        string expected,
        string actual)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2133", span,
          $"Literal pattern has type '{actual}', but the matched value has type '{expected}'.",
          "Use a literal with exactly the scrutinee type."));
    }

    public void ReportDuplicatePatternBinding(TextSpan span, string name)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2134", span,
          $"Pattern binding '{name}' is declared more than once in this pattern.",
          "Use a unique binding name or '_' for an ignored payload."));
    }

    public void ReportMatchArmTypeMismatch(
        TextSpan span,
        string expected,
        string actual)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2135", span,
          $"Match arm has type '{actual}', but previous reachable arms have type '{expected}'.",
          "Return the same type from every reachable arm; Never arms are compatible."));
    }

    public void ReportEnumPatternRequiresMatchingEnum(
        TextSpan span,
        string actual)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2136", span,
          $"Enum variant pattern cannot match value of type '{actual}'.",
          "Match an enum value or replace the enum pattern with a supported literal/wildcard."));
    }

    public void ReportEnumPatternFormMismatch(
        TextSpan span,
        string pattern,
        string expectedForm)
    {
      Report(new Diagnostic(DiagnosticSeverity.Error, "SBK2137", span,
          $"Enum variant pattern '{pattern}' must use {expectedForm} pattern syntax.",
          "Use the pattern form matching the variant declaration."));
    }

    public void ReportAmbiguousReExport(
        TextSpan span,
        string name,
        string first,
        string second)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4024",
          span,
          $"Ambiguous re-exported name '{name}': {first}, {second}.",
          "Use an as alias or remove one re-export."
      ));
    }

    public void ReportModuleMemberNotPublic(TextSpan span, string module, string member)
    {
      Report(new Diagnostic(
          DiagnosticSeverity.Error,
          "SBK4025",
          span,
          $"Member '{member}' is private in module '{module}'.",
          "Make the declaration public or use a public re-export."
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
