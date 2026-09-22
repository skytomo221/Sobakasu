using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using DiagnosticItem = Skytomo221.Sobakasu.Compiler.Diagnostic.Diagnostic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    public static class DeclarationDiagnosticExtensions
    {
        public static void ReportUnknownEvent(this DiagnosticBag diagnostics, TextSpan span, string eventName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2031",
                span,
                $"Unknown event '{eventName}'.",
                "Use an event name listed in the Sobakasu event catalog."
            ));
        }

        public static void ReportDuplicateEvent(this DiagnosticBag diagnostics, TextSpan span, string eventName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2032",
                span,
                $"Event '{eventName}' is already declared in this file.",
                "Declare each event at most once."
            ));
        }

        public static void ReportUnsupportedEventSignature(this DiagnosticBag diagnostics, TextSpan span, string eventName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2033",
                span,
                $"Event '{eventName}' is known but its signature is not supported yet.",
                "Wait for this Unity event signature to be confirmed before using it."
            ));
        }

        public static void ReportEventParameterCountMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string eventName,
            int expected,
            int actual)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2034",
                span,
                $"Event '{eventName}' expects {expected} parameter(s), but got {actual}.",
                "Match the event parameter count defined by the event catalog."
            ));
        }

        public static void ReportEventParameterTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string eventName,
            int parameterIndex,
            string expectedType,
            string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2035",
                span,
                $"Event '{eventName}' parameter {parameterIndex + 1} must be '{expectedType}', but got '{actualType}'.",
                "Use the exact parameter type required by the event catalog."
            ));
        }

        public static void ReportEventReturnTypeRequired(this DiagnosticBag diagnostics, TextSpan span,
            string eventName,
            string returnType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2036",
                span,
                $"Event '{eventName}' must declare return type '{returnType}'.",
                "Add an explicit return type annotation to this event declaration."
            ));
        }

        public static void ReportEventReturnTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string eventName,
            string expectedType,
            string actualType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2037",
                span,
                $"Event '{eventName}' must return '{expectedType}', but declares '{actualType}'.",
                "Make the event return annotation match the event catalog."
            ));
        }

        public static void ReportDuplicateParameterName(this DiagnosticBag diagnostics, TextSpan span, string parameterName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2041",
                span,
                $"Parameter '{parameterName}' is already declared for this declaration.",
                "Use a unique parameter name."
            ));
        }

        public static void ReportEventRequiresComponent(this DiagnosticBag diagnostics, TextSpan span,
            string eventName,
            string requirement)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Warning,
                "SBK2042",
                span,
                $"Event '{eventName}' requires component '{requirement}'.",
                "Ensure the corresponding component is present on the UdonBehaviour GameObject."
            ));
        }

        public static void ReportDuplicateFunctionName(this DiagnosticBag diagnostics, TextSpan span, string functionName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2043",
                span,
                $"Function '{functionName}' is already declared in this file.",
                "Declare each user-defined function at most once."
            ));
        }

        public static void ReportDuplicateFunctionOverload(this DiagnosticBag diagnostics, TextSpan span, string signature)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2154",
                span,
                $"Duplicate function overload '{signature}'.",
                "Change the function name or one of its parameter types; return types do not distinguish overloads."
            ));
        }

        public static void ReportExternalFunctionBindingRequiresMember(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2157",
                span,
                "A declarative extern binding must resolve to one external member.",
                "Bind a method, property, field, constructor, or catalog-backed operator exposed by Udon."
            ));
        }

        public static void ReportMaybeExternalBindingUnsupported(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string reason)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2158",
                span,
                $"Cannot create a maybe extern binding for external result type '{type}'. {reason}",
                "Use a supported reference-returning extern member or declare a raw '= extern ...' binding."
            ));
        }

        public static void ReportExternalBindingReturnTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string declared,
            string resolved)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2159",
                span,
                $"Declarative extern binding return type '{declared}' is incompatible with resolved external return type '{resolved}'.",
                "Use the resolved Sobakasu return type or omit the return annotation to infer it."
            ));
        }

        public static void ReportMaybeExternalBindingReturnTypeMismatch(this DiagnosticBag diagnostics, TextSpan span,
            string declared,
            string resolved)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2160",
                span,
                $"Maybe extern binding return type '{declared}' does not match '{resolved}'.",
                "Use Maybe<resolved type> or omit the return annotation to infer it."
            ));
        }

        public static void ReportMaybeOutExternalBindingUnsupported(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string reason)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2164",
                span,
                $"Cannot project extern out parameter type '{type}' to Maybe. {reason}",
                "Use 'maybe out' only with a validity-checkable reference output, or use raw 'out'."
            ));
        }

        public static void ReportMissingStateInitializer(this DiagnosticBag diagnostics, TextSpan span, string stateName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2056",
                span,
                $"Top-level state '{stateName}' requires an initializer.",
                "Add '= <compile-time constant>' to the declaration."
            ));
        }

        public static void ReportCannotInferStateType(this DiagnosticBag diagnostics, TextSpan span, string stateName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2057",
                span,
                $"Cannot infer the type of top-level state '{stateName}'.",
                "Add an explicit type annotation with a compatible constant initializer."
            ));
        }

        public static void ReportDuplicateState(this DiagnosticBag diagnostics, TextSpan span, string stateName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2058",
                span,
                $"Top-level state '{stateName}' is already declared in this file.",
                "Declare each top-level state name at most once."
            ));
        }

        public static void ReportSynchronizedStateMustBeMutable(this DiagnosticBag diagnostics, TextSpan span, string stateName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2060",
                span,
                $"Synchronized state binding '{stateName}' must be mutable.",
                $"Write 'sync let mut {stateName} = <value>;'."
            ));
        }

        public static void ReportUnsupportedStateSynchronization(this DiagnosticBag diagnostics, TextSpan span,
            string stateName,
            string mode,
            string typeName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2061",
                span,
                $"State '{stateName}' of type '{typeName}' is not supported for {mode} synchronization.",
                "Choose a synchronization mode supported by the SDK for this type."
            ));
        }

        public static void ReportStateInitializerMustBeConstant(this DiagnosticBag diagnostics, TextSpan span, string stateName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2062",
                span,
                $"Top-level initializer for state '{stateName}' must be a compile-time constant.",
                "Use a literal, null for a reference type, or a supported unary constant expression."
            ));
        }

        public static void ReportStateNameConflict(this DiagnosticBag diagnostics, TextSpan span, string stateName, string otherKind)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2063",
                span,
                $"Top-level state '{stateName}' conflicts with a {otherKind} of the same name.",
                "Rename one of the top-level declarations."
            ));
        }

        public static void ReportUnknownImplTarget(this DiagnosticBag diagnostics, TextSpan span, string typeName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2065",
                span,
                $"Unknown impl target type '{typeName}'.",
                "Declare or import the Sobakasu type before adding an impl block."
            ));
        }

        public static void ReportUnknownExternalType(this DiagnosticBag diagnostics, TextSpan span, string typeName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2066",
                span,
                $"Unknown external type '{typeName}'.",
                "Use a fully-qualified CLR type name available to the Unity Editor."
            ));
        }

        public static void ReportExternalTypeNotExposed(this DiagnosticBag diagnostics, TextSpan span, string typeName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2067",
                span,
                $"External type '{typeName}' is not exposed to Udon.",
                "Bind only runtime types supported by the installed VRChat SDK."
            ));
        }

        public static void ReportDuplicateExternalTypeBinding(this DiagnosticBag diagnostics, TextSpan span, string typeName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2068",
                span,
                $"Duplicate external type binding for Sobakasu type '{typeName}'.",
                "Keep exactly one external binding declaration for this type."
            ));
        }

        public static void ReportExternalRuntimeTypeAlreadyBound(this DiagnosticBag diagnostics, TextSpan span,
            string runtimeType,
            string existingType)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2069",
                span,
                $"External runtime type '{runtimeType}' is already bound as '{existingType}'.",
                "Reuse the existing Sobakasu type instead of creating another binding."
            ));
        }

        public static void ReportCannotExternallyBindBuiltInType(this DiagnosticBag diagnostics, TextSpan span, string typeName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2070",
                span,
                $"Built-in type '{typeName}' cannot be externally bound.",
                "Use a normal impl block to add methods to a built-in type."
            ));
        }

        public static void ReportDuplicateMethodSignature(this DiagnosticBag diagnostics, TextSpan span, string methodName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2071",
                span,
                $"Duplicate method signature for '{methodName}'.",
                "Change the method name or one of its explicit parameter types."
            ));
        }

        public static void ReportSelfParameterMustBeFirst(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2072",
                span,
                "The receiver parameter 'self' must be the first parameter of an impl function.",
                "Move 'self' to the first parameter position or remove it."
            ));
        }

        public static void ReportSelfParameterOutsideImpl(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2087",
                span,
                "The receiver parameter 'self' is only valid in an impl function.",
                "Use a typed parameter in a top-level function."
            ));
        }

        public static void ReportInvalidOperatorName(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2075",
                span,
                $"Invalid operator declaration '{name}'.",
                "Declare operators only as instance functions inside impl."
            ));
        }

        public static void ReportOperatorCannotBeOverloaded(this DiagnosticBag diagnostics, TextSpan span, string operatorText)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2076",
                span,
                $"Operator '{operatorText}' cannot be overloaded.",
                "Short-circuit, assignment, and compound-assignment operators are compiler-defined."
            ));
        }

        public static void ReportInvalidUnaryOperatorArity(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2077",
                span,
                $"Unary operator '{name}' must not have explicit parameters.",
                "The operand is supplied through the implicit self receiver."
            ));
        }

        public static void ReportInvalidBinaryOperatorArity(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2078",
                span,
                $"Binary operator '{name}' must have exactly one explicit parameter.",
                "The left operand is supplied through the implicit self receiver."
            ));
        }

        public static void ReportComparisonOperatorMustReturnBool(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2079",
                span,
                $"Comparison operator '{name}' must return bool.",
                "Change the declared return type to bool."
            ));
        }

        public static void ReportPublicModifierNotAllowedOnAdditionalImpl(this DiagnosticBag diagnostics, TextSpan span)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2088",
                span,
                "pub is not allowed on an additional impl block.",
                "Put pub on individual methods; type visibility belongs to its external binding."
            ));
        }

        public static void ReportInvalidExternalBindingTarget(this DiagnosticBag diagnostics, TextSpan span, string typeName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2089",
                span,
                $"External binding target '{typeName}' must be a new simple Sobakasu type name.",
                "Use one identifier on the left side of '= extern'."
            ));
        }

        public static void ReportUnsupportedObjectStateInitializer(this DiagnosticBag diagnostics, TextSpan span, string stateName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2090",
                span,
                $"Top-level object state '{stateName}' does not support a source initializer with the current heap-patch format.",
                "Use Maybe<object> with Maybe.Nothing for optional state, or initialize the object value at runtime."
            ));
        }

        public static void ReportPublicArrayTypeNotAvailable(this DiagnosticBag diagnostics, TextSpan span, string typeName)
        {
            diagnostics.Report(new DiagnosticItem(
                DiagnosticSeverity.Error,
                "SBK2100",
                span,
                $"Array type '{typeName}' cannot be exposed as a public Udon variable.",
                "Use an array ABI type supported by the installed SDK Inspector."
            ));
        }

        public static void ReportDuplicateAggregateType(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2101", span,
                $"Type '{name}' is declared more than once.",
                "Use a unique name for each struct, enum, and external type binding."));
        }

        public static void ReportDuplicateAggregateField(this DiagnosticBag diagnostics, TextSpan span, string type, string field)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2102", span,
                $"Struct '{type}' declares field '{field}' more than once.",
                "Remove or rename the duplicate field."));
        }

        public static void ReportDuplicateEnumVariant(this DiagnosticBag diagnostics, TextSpan span, string type, string variant)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2103", span,
                $"Enum '{type}' declares variant '{variant}' more than once.",
                "Remove or rename the duplicate variant."));
        }

        public static void ReportDuplicateEnumPayloadField(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string variant,
            string field)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2104", span,
                $"Enum variant '{type}.{variant}' declares payload field '{field}' more than once.",
                "Remove or rename the duplicate payload field."));
        }

        public static void ReportUnsupportedAggregateSynchronization(this DiagnosticBag diagnostics, TextSpan span,
            string type,
            string path,
            string leafType,
            string mode)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2118", span,
                $"Cannot synchronize '{type}' because field path '{path}' has unsupported type '{leafType}' for mode '{mode}'.",
                "Change the field type or remove/change the synchronization mode."));
        }

        public static void ReportDuplicateGenericParameter(this DiagnosticBag diagnostics, TextSpan span,
            string declaration,
            string parameter)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2120", span,
                $"Generic declaration '{declaration}' declares type parameter '{parameter}' more than once.",
                "Use a unique name for each type parameter."));
        }

        public static void ReportInvalidGenericImplTarget(this DiagnosticBag diagnostics, TextSpan span, string type)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2125", span,
                $"Generic impl target '{type}' must apply every impl type parameter exactly once to one generic aggregate definition.",
                "Use a target such as 'Box<T>' or 'Pair<T, U>' without specialization."));
        }

        public static void ReportDuplicateNetworkReceiver(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2138", span,
                $"Network receiver '{name}' is declared more than once.",
                "Use a unique receive name; network receivers cannot be overloaded."));
        }

        public static void ReportUnsupportedNetworkParameter(this DiagnosticBag diagnostics, TextSpan span,
            string receiver,
            string path,
            string type)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2139", span,
                $"Network receiver '{receiver}' parameter '{path}' has unsupported network type '{type}'.",
                "Use a scalar or array type supported by the installed VRChat network serialization ABI."));
        }

        public static void ReportNetworkPhysicalParameterLimit(this DiagnosticBag diagnostics, TextSpan span,
            string receiver,
            int actual)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2140", span,
                $"Network receiver '{receiver}' lowers to {actual} physical parameter(s); the installed SDK supports at most 8.",
                "Reduce the number of parameters or aggregate leaves to 8 or fewer."));
        }

        public static void ReportNetworkEntrypointCollision(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2146", span,
                $"Generated Udon entry point '{name}' conflicts with another event or receiver.",
                "Rename the receive declaration so every exported entry point is unique."));
        }

        public static void ReportUnsupportedNetworkAggregate(this DiagnosticBag diagnostics, TextSpan span, string type)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2147", span,
                $"Aggregate network parameter type '{type}' cannot be flattened safely.",
                "Use a struct with network-compatible leaves; payload enums and aggregate arrays are not supported."));
        }

        public static void ReportDuplicateConstant(this DiagnosticBag diagnostics, TextSpan span, string name)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2148", span,
                $"Constant '{name}' is declared more than once in this module.",
                "Use a unique constant name."));
        }

        public static void ReportTopLevelDeclarationNameConflict(this DiagnosticBag diagnostics, TextSpan span,
            string name,
            string otherKind)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2149", span,
                $"Top-level declaration '{name}' conflicts with a {otherKind} of the same name.",
                "Rename one of the top-level declarations."));
        }

        public static void ReportExternalAggregateKindMismatch(this DiagnosticBag diagnostics, TextSpan span, string name, string kind, string runtimeType)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2164", span,
                $"External {kind} '{name}' cannot bind CLR type '{runtimeType}'.",
                "Bind enum declarations to CLR enums and struct declarations to non-enum CLR value types."));
        }

        public static void ReportExternalAggregateMemberBindingRequired(this DiagnosticBag diagnostics, TextSpan span, string type, string member)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2165", span,
                $"External aggregate member '{type}.{member}' requires '= extern ExternalName'.",
                "Declare an explicit external member name."));
        }

        public static void ReportExternalAggregateMemberOnNormalType(this DiagnosticBag diagnostics, TextSpan span, string type, string member)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2166", span,
                $"Normal aggregate member '{type}.{member}' cannot have an external binding.",
                "Remove '= extern ...' or make the containing type an external aggregate binding."));
        }

        public static void ReportExternalEnumPayloadNotAllowed(this DiagnosticBag diagnostics, TextSpan span, string type, string variant)
        {
            diagnostics.Report(new DiagnosticItem(DiagnosticSeverity.Error, "SBK2167", span,
                $"External enum variant '{type}.{variant}' cannot have a payload.",
                "Use a unit variant bound to a CLR enum member."));
        }
    }
}
