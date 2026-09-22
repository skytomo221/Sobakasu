using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class ExpressionDiagnosticExtensions
    {
        public static void ReportUndefinedName(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2002",
                span,
                $"Undefined name '{name}'.",
                "Declare the symbol before using it."
            ));
        }

        public static void ReportInvalidArgumentCount(this DiagnosticBag diagnostics, TextSpan span, string callableName, int expected, int actual)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2004",
                span,
                $"'{callableName}' expects {expected} argument(s), but got {actual}.",
                "Adjust the argument count to match the callable signature."
            ));
        }

        public static void ReportUnsupportedExpression(this DiagnosticBag diagnostics, TextSpan span, string expressionKind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2007",
                span,
                $"Unsupported expression '{expressionKind}'.",
                "Use an expression form that the compiler currently supports."
            ));
        }

        public static void ReportUnsupportedCallTarget(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2008",
                span,
                "Only member call expressions are supported as call targets.",
                "Use a supported member call such as Debug.Log(...)."
            ));
        }

        public static void ReportCannotInferArrayType(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2010",
                span,
                "Cannot infer the element type of this array literal.",
                "Add an explicit array type, such as 'let values: [i32] = [];', or provide a typed element."
            ));
        }

        public static void ReportArrayElementTypeMismatch(this DiagnosticBag diagnostics, TextSpan span, string expectedType, string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2011",
                span,
                $"Array literal element type '{actualType}' does not match '{expectedType}'.",
                "All array literal elements must share a single element type."
            ));
        }

        public static void ReportCallTargetIsNotMethod(this DiagnosticBag diagnostics, TextSpan span, string targetName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2012",
                span,
                $"'{targetName}' is not a method.",
                "Call a resolved method symbol instead of a non-callable expression."
            ));
        }

        public static void ReportNoMatchingOverload(this DiagnosticBag diagnostics, TextSpan span, string callableName, string argumentTypes)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2013",
                span,
                $"No overload of '{callableName}' matches argument type(s): {argumentTypes}.",
                "Adjust the argument types so they match one of the available overloads."
            ));
        }

        public static void ReportCannotAssignToImmutableLocal(this DiagnosticBag diagnostics, TextSpan span, string variableName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2016",
                span,
                $"Cannot assign to immutable local '{variableName}'.",
                "Add 'mut' to the declaration if reassignment is required."
            ));
        }

        public static void ReportInvalidAssignmentTarget(this DiagnosticBag diagnostics, TextSpan span, string targetName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2017",
                span,
                $"'{targetName}' is not an assignable local variable.",
                "Assign only to a previously declared local variable."
            ));
        }

        public static void ReportNoCallableExternCandidate(this DiagnosticBag diagnostics, TextSpan span, string callableName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2022",
                span,
                $"No callable extern candidates were found for '{callableName}'.",
                "Import or call a method group that contains at least one callable Udon extern."
            ));
        }

        public static void ReportAmbiguousExternOverload(this DiagnosticBag diagnostics, TextSpan span,
            string callableName,
            string candidates)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2023",
                span,
                $"Call to '{callableName}' is ambiguous between overloads: {candidates}.",
                "Adjust the argument types or import a less ambiguous callable."
            ));
        }

        public static void ReportExternCandidatesNotUdonCallable(this DiagnosticBag diagnostics, TextSpan span,
            string callableName,
            string details)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2024",
                span,
                $"Extern candidates were discovered for '{callableName}', but none are callable as Udon externs. {details}",
                "Use a Udon-exposed API surface or change the import/call target."
            ));
        }

        public static void ReportInvalidCompoundAssignmentTarget(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2029",
                span,
                "Compound assignment requires a mutable local variable target in v1.",
                "Use a mutable local variable on the left-hand side."
            ));
        }

        public static void ReportNoMatchingFunctionOverload(this DiagnosticBag diagnostics, TextSpan span,
            string functionName,
            string argumentTypes,
            string candidates)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2155",
                span,
                $"No matching overload for '{functionName}({argumentTypes})'. Candidates: {candidates}.",
                "Adjust the argument count or types to match one of the available function overloads."
            ));
        }

        public static void ReportAmbiguousFunctionOverload(this DiagnosticBag diagnostics, TextSpan span,
            string functionName,
            string argumentTypes,
            string candidates)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2156",
                span,
                $"Ambiguous function overload for '{functionName}({argumentTypes})'. Candidates: {candidates}.",
                "Use argument types that select one overload with a strictly better conversion rank."
            ));
        }

        public static void ReportAmbiguousUserFunctionExternCall(this DiagnosticBag diagnostics, TextSpan span, string functionName, string externCandidate)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2044",
                span,
                $"Call to '{functionName}' is ambiguous between a user-defined function and extern '{externCandidate}'.",
                "Rename the function or the import alias so the call target is unambiguous."
            ));
        }

        public static void ReportFirstClassFunctionValueNotSupported(this DiagnosticBag diagnostics, TextSpan span, string functionName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2046",
                span,
                $"Function '{functionName}' cannot be used as a value in v1.",
                "Call the function directly instead of storing or passing it as a value."
            ));
        }

        public static void ReportConditionRequiresBool(this DiagnosticBag diagnostics, TextSpan span,
            string constructName,
            string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2047",
                span,
                $"The '{constructName}' condition must have type 'bool', but got '{actualType}'.",
                "Use a bool expression; Sobakasu does not apply truthy/falsy conversion."
            ));
        }

        public static void ReportIfValueRequiresElse(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2048",
                span,
                "An if expression without else cannot produce a value.",
                "Add an else branch with the same result type, or make the then branch return ()."
            ));
        }

        public static void ReportIfBranchTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string thenType,
            string elseType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2049",
                span,
                $"If branch types do not match: then is '{thenType}', else is '{elseType}'.",
                "Return exactly the same type from every reachable branch."
            ));
        }

        public static void ReportCannotAssignToImmutableState(this DiagnosticBag diagnostics, TextSpan span, string stateName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2059",
                span,
                $"Cannot assign to immutable state '{stateName}'.",
                "Add 'mut' to the top-level state declaration if reassignment is required."
            ));
        }

        public static void ReportCallableRequiresArguments(this DiagnosticBag diagnostics, TextSpan span,
            string callableName,
            int requiredArgumentCount)
        {
            var countText = requiredArgumentCount < 0
                ? "one or more arguments"
                : $"{requiredArgumentCount} argument(s)";
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2064",
                span,
                $"Callable '{callableName}' requires {countText}; parentheses can only be omitted for a zero-argument call.",
                $"Call it as '{callableName}(...)'."
            ));
        }

        public static void ReportSelfUnavailableInAssociatedFunction(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2073",
                span,
                "self is unavailable in an associated function.",
                "Declare 'self' as the first parameter to make this an instance method."
            ));
        }

        public static void ReportAssociatedMemberRequiresPath(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK3068",
                span,
                "Associated items must be accessed with '::', not '.'.",
                "Use 'Type::member' for an associated item and 'value.member' for an instance member."
            ));
        }

        public static void ReportPathRequiresModuleOrType(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK3069",
                span,
                "The left side of '::' must resolve to a module, namespace, or type.",
                "Use '.' for a member on a value."
            ));
        }

        public static void ReportNoApplicableMethodOverload(this DiagnosticBag diagnostics, TextSpan span,
            string methodName,
            string argumentTypes)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2081",
                span,
                $"No applicable method overload for '{methodName}' with argument types {argumentTypes}.",
                "Check the argument count and types."
            ));
        }

        public static void ReportAmbiguousMethodOverload(this DiagnosticBag diagnostics, TextSpan span,
            string methodName,
            string candidates)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2082",
                span,
                $"Ambiguous method overload for '{methodName}'. Candidates: {candidates}.",
                "Use argument types that select one overload exactly."
            ));
        }

        public static void ReportArrayTypeNotAvailable(this DiagnosticBag diagnostics, TextSpan span,
            string typeName,
            string reason)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2091",
                span,
                $"Array type '{typeName}' is not available on the installed Udon target. {reason}",
                "Use an element array ABI type exposed by the installed VRChat SDK."
            ));
        }

        public static void ReportUnresolvedArrayRepeatOperand(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2092",
                span,
                "The left side of this repeat array is neither a resolvable type nor a value expression.",
                "Use '[Type; length]' for default values or '[expression; length]' for repeated evaluation."
            ));
        }

        public static void ReportAmbiguousArrayRepeatOperand(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2093",
                span,
                "The left side of this repeat array is ambiguous between a type and a value.",
                "Rename the value binding or use an unambiguous qualified type name."
            ));
        }

        public static void ReportInvalidArrayLengthType(this DiagnosticBag diagnostics, TextSpan span,
            string expectedType,
            string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2094",
                span,
                $"Array length must have type '{expectedType}', but got '{actualType}'.",
                $"Convert or rewrite the length expression as '{expectedType}'."
            ));
        }

        public static void ReportNegativeArrayLength(this DiagnosticBag diagnostics, TextSpan span, int length)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2095",
                span,
                $"Array length cannot be negative ({length}).",
                "Use a non-negative i32 length."
            ));
        }

        public static void ReportIndexTargetIsNotArray(this DiagnosticBag diagnostics, TextSpan span, string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2096",
                span,
                $"A value of type '{actualType}' cannot be indexed as an array.",
                "Use indexing only on a value whose type is '[T]'."
            ));
        }

        public static void ReportInvalidArrayIndexType(this DiagnosticBag diagnostics, TextSpan span,
            string expectedType,
            string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2097",
                span,
                $"Array index must have type '{expectedType}', but got '{actualType}'.",
                $"Convert or rewrite the index expression as '{expectedType}'."
            ));
        }

        public static void ReportArrayElementAssignmentTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string expectedType,
            string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2098",
                span,
                $"Cannot assign value of type '{actualType}' to array element type '{expectedType}'.",
                "Make the assigned value compatible with the array element type."
            ));
        }

        public static void ReportUnsupportedArrayElementCompoundAssignment(this DiagnosticBag diagnostics, TextSpan span,
            string operatorText,
            string elementType,
            string valueType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2099",
                span,
                $"Operator '{operatorText}' is not available for array element type '{elementType}' and value type '{valueType}'.",
                "Use a compound operator supported by the element and right-hand-side types."
            ));
        }

        public static void ReportUnknownAggregateInitializerField(this DiagnosticBag diagnostics, TextSpan span,
            string target,
            string field)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2106", span,
                $"Initializer for '{target}' has no field named '{field}'.",
                "Use one of the fields declared by the aggregate type."));
        }

        public static void ReportMissingAggregateInitializerField(this DiagnosticBag diagnostics, TextSpan span,
            string target,
            string field)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2107", span,
                $"Initializer for '{target}' is missing required field '{field}'.",
                "Specify every aggregate field; field defaults are not supported."));
        }

        public static void ReportDuplicateAggregateInitializerField(this DiagnosticBag diagnostics, TextSpan span,
            string target,
            string field)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2108", span,
                $"Initializer for '{target}' specifies field '{field}' more than once.",
                "Specify each aggregate field exactly once."));
        }

        public static void ReportAggregateInitializerTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string target,
            string field,
            string expected,
            string actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2109", span,
                $"Field '{target}.{field}' expects '{expected}', but got '{actual}'.",
                "Use a value compatible with the declared field type."));
        }

        public static void ReportStructInitializerRequiresStruct(this DiagnosticBag diagnostics, TextSpan span, string type)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2110", span,
                $"Struct initializer syntax cannot construct '{type}'.",
                "Use a user-defined struct type or a struct enum variant."));
        }

        public static void ReportUnknownEnumVariant(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string variant)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2111", span,
                $"Enum '{type}' has no variant named '{variant}'.",
                "Use a variant declared by the enum."));
        }

        public static void ReportEnumVariantRequiresPayload(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string variant)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2112", span,
                $"Enum variant '{type}.{variant}' requires a payload.",
                "Use tuple call syntax or struct initializer syntax for this variant."));
        }

        public static void ReportEnumVariantConstructionForm(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string variant,
            string actualForm)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2113", span,
                $"Enum variant '{type}.{variant}' cannot be constructed with {actualForm} syntax.",
                "Use the construction syntax matching the variant declaration."));
        }

        public static void ReportEnumTuplePayloadArity(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string variant,
            int expected,
            int actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2114", span,
                $"Enum variant '{type}.{variant}' expects {expected} payload value(s), but got {actual}.",
                "Pass exactly the declared number of tuple payload values."));
        }

        public static void ReportEnumTuplePayloadTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string variant,
            int index,
            string expected,
            string actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2115", span,
                $"Payload {index} of '{type}.{variant}' expects '{expected}', but got '{actual}'.",
                "Use a value compatible with the declared payload type."));
        }

        public static void ReportNonExhaustiveMatch(this DiagnosticBag diagnostics, TextSpan span, string missing)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2126", span,
                $"Non-exhaustive match: {missing} is not covered.",
                "Add the missing pattern or a wildcard '_' arm."));
        }

        public static void ReportUnreachableMatchArm(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2127", span,
                "Unreachable match arm.",
                "Remove the arm or place it before the pattern that already covers it."));
        }

        public static void ReportEnumVariantBelongsToDifferentEnum(this DiagnosticBag diagnostics, TextSpan span,
            string pattern,
            string expectedEnum)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2128", span,
                $"Enum variant pattern '{pattern}' does not belong to matched enum '{expectedEnum}'.",
                "Use a variant declared by the scrutinee's enum type."));
        }

        public static void ReportMatchTuplePatternArity(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string variant,
            int expected,
            int actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2129", span,
                $"Enum pattern '{type}.{variant}' expects {expected} payload binding(s), but got {actual}.",
                "Bind or discard every tuple payload position exactly once."));
        }

        public static void ReportUnknownStructVariantPatternField(this DiagnosticBag diagnostics, TextSpan span,
            string pattern,
            string field)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2130", span,
                $"Enum struct pattern '{pattern}' has no payload field named '{field}'.",
                "Use a field declared by the enum variant."));
        }

        public static void ReportDuplicateStructVariantPatternField(this DiagnosticBag diagnostics, TextSpan span,
            string pattern,
            string field)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2131", span,
                $"Enum struct pattern '{pattern}' specifies field '{field}' more than once.",
                "Specify each payload field exactly once."));
        }

        public static void ReportMissingStructVariantPatternField(this DiagnosticBag diagnostics, TextSpan span,
            string pattern,
            string field)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2132", span,
                $"Enum struct pattern '{pattern}' is missing payload field '{field}'.",
                "Specify every payload field; rest patterns are not supported."));
        }

        public static void ReportLiteralPatternTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string expected,
            string actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2133", span,
                $"Literal pattern has type '{actual}', but the matched value has type '{expected}'.",
                "Use a literal with exactly the scrutinee type."));
        }

        public static void ReportDuplicatePatternBinding(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2134", span,
                $"Pattern binding '{name}' is declared more than once in this pattern.",
                "Use a unique binding name or '_' for an ignored payload."));
        }

        public static void ReportMatchArmTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string expected,
            string actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2135", span,
                $"Match arm has type '{actual}', but previous reachable arms have type '{expected}'.",
                "Return the same type from every reachable arm; Never arms are compatible."));
        }

        public static void ReportEnumPatternRequiresMatchingEnum(this DiagnosticBag diagnostics, TextSpan span,
            string actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2136", span,
                $"Enum variant pattern cannot match value of type '{actual}'.",
                "Match an enum value or replace the enum pattern with a supported literal/wildcard."));
        }

        public static void ReportEnumPatternFormMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string pattern,
            string expectedForm)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2137", span,
                $"Enum variant pattern '{pattern}' must use {expectedForm} pattern syntax.",
                "Use the pattern form matching the variant declaration."));
        }

        public static void ReportTupleIndexOutOfRange(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            int index,
            int elementCount)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2161", span,
                $"Tuple index {index} is outside '{type}', which has {elementCount} element(s).",
                "Use an index between 0 and the tuple element count minus one."));
        }
    }
}
