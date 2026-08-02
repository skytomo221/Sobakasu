using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;

namespace Skytomo221.Sobakasu.Compiler.Diagnostic
{
  public class DiagnosticBag
  {
    private readonly List<Diagnostic> _diagnostics = new();

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

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
          "In this version, pub and sync can only modify top-level state declarations."
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
