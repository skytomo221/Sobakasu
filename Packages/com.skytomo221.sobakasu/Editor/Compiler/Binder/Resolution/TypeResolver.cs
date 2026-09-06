using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class TypeResolver : BinderComponent
    {
        internal TypeResolver(BindingSession session) : base(session)
        {
        }

        internal static readonly IReadOnlyDictionary<string, TypeSymbol> BuiltInTypes = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal)
        {
            ["i8"] = TypeSymbol.I8,
            ["u8"] = TypeSymbol.U8,
            ["i16"] = TypeSymbol.I16,
            ["u16"] = TypeSymbol.U16,
            ["i32"] = TypeSymbol.I32,
            ["u32"] = TypeSymbol.U32,
            ["i64"] = TypeSymbol.I64,
            ["u64"] = TypeSymbol.U64,
            ["f32"] = TypeSymbol.F32,
            ["f64"] = TypeSymbol.F64,
            ["char"] = TypeSymbol.Char,
            ["string"] = TypeSymbol.String,
            ["bool"] = TypeSymbol.Bool,
            ["object"] = TypeSymbol.Object
        };
        internal TypeSymbol BindTypeClause(TypeClauseSyntax syntax)
        {
            return Session.TypeResolver.BindTypeSyntax(syntax.Type);
        }

        internal TypeSymbol BindTypeSyntax(TypeSyntax syntax)
        {
            if (syntax.IsTuple)
            {
                var elements = new TypeSymbol[syntax.TupleElementTypes.Count];
                for (var index = 0; index < elements.Length; index++)
                    elements[index] = Session.TypeResolver.BindTypeSyntax(syntax.TupleElementTypes[index]);
                return Session.TypeResolver.ContainsTypeError(elements) ? TypeSymbol.Error : TypeSymbol.Tuple(elements);
            }

            if (syntax.IsArray)
            {
                var elementType = Session.TypeResolver.BindTypeSyntax(syntax.ElementType);
                if (elementType == TypeSymbol.Error)
                    return TypeSymbol.Error;
                if (elementType.ContainsGenericParameters)
                    return TypeSymbol.Array(elementType);
                return Session.ExpressionBinder.BindArrayType(elementType, syntax.GetSpan(), out _);
            }

            var typeName = syntax.GetNameText();
            if (string.Equals(typeName, "Self", StringComparison.Ordinal))
            {
                if (Session.Body.CurrentType != null)
                    return Session.TypeResolver.ApplyTypeArguments(Session.Body.CurrentType, syntax);
                Session.Diagnostics.ReportSelfTypeOutsideImpl(syntax.GetSpan());
                return TypeSymbol.Error;
            }

            if (Session.Generics.CurrentTypeParameters.TryGetValue(typeName, out var genericParameter))
                return Session.TypeResolver.ApplyTypeArguments(genericParameter, syntax);
            if (TypeResolver.BuiltInTypes.TryGetValue(typeName, out var builtInType))
                return Session.TypeResolver.ApplyTypeArguments(builtInType, syntax);
            if (Session.NameResolver.TryGetCurrentModuleType(typeName, out var declaredType))
                return Session.TypeResolver.ApplyTypeArguments(declaredType, syntax);
            var span = syntax.GetSpan();
            if (typeName.IndexOf('.', StringComparison.Ordinal) >= 0)
            {
                if (Session.TypeResolver.TryResolveModuleType(syntax, out var moduleType))
                    return Session.TypeResolver.ApplyTypeArguments(moduleType, syntax);
                if (Session.Environment.ExternCatalog.TryGetTypeSymbol(typeName, out var qualifiedTypeSymbol))
                    return Session.TypeResolver.ApplyTypeArguments(qualifiedTypeSymbol, syntax);
                Session.Diagnostics.ReportUnknownType(span, typeName);
                return TypeSymbol.Error;
            }

            var resolvedSymbol = Session.NameResolver.ResolveVisibleSymbol(typeName, span, out var resolutionHadDiagnostic);
            if (resolvedSymbol is TypeSymbol typeSymbol)
                return Session.TypeResolver.ApplyTypeArguments(typeSymbol, syntax);
            if (resolutionHadDiagnostic)
                return TypeSymbol.Error;
            if (EventCatalog.TryGetKnownType(typeName, out var eventType))
                return Session.TypeResolver.ApplyTypeArguments(Session.TypeResolver.ResolveCanonicalType(eventType), syntax);
            Session.Diagnostics.ReportUnknownType(span, typeName);
            return TypeSymbol.Error;
        }

        internal TypeSymbol ApplyTypeArguments(TypeSymbol type, TypeSyntax syntax)
        {
            var argumentSyntax = syntax.TypeArgumentList;
            var actualArity = argumentSyntax?.Arguments.Count ?? 0;
            var expectedArity = type.IsGenericDefinition ? type.GenericParameters.Count : 0;
            if (argumentSyntax == null)
            {
                if (expectedArity == 0)
                    return type;
                Session.Diagnostics.ReportWrongGenericArity(syntax.GetSpan(), type.Name, expectedArity, 0);
                return TypeSymbol.Error;
            }

            if (!type.IsGenericDefinition || actualArity != expectedArity)
            {
                Session.Diagnostics.ReportWrongGenericArity(syntax.GetSpan(), type.Name, expectedArity, actualArity);
                foreach (var argument in argumentSyntax.Arguments)
                    Session.TypeResolver.BindTypeSyntax(argument);
                return TypeSymbol.Error;
            }

            var arguments = Session.TypeResolver.BindTypeArguments(argumentSyntax);
            if (Session.TypeResolver.ContainsTypeError(arguments))
                return TypeSymbol.Error;
            return type.Construct(arguments);
        }

        internal IReadOnlyList<TypeSymbol> BindTypeArguments(TypeArgumentListSyntax syntax)
        {
            var arguments = new List<TypeSymbol>();
            foreach (var argument in syntax.Arguments)
                arguments.Add(Session.TypeResolver.BindTypeSyntax(argument));
            return arguments;
        }

        internal bool ContainsTypeError(IReadOnlyList<TypeSymbol> types)
        {
            foreach (var type in types)
            {
                if (type == TypeSymbol.Error)
                    return true;
            }

            return false;
        }

        internal bool TryResolveModuleType(TypeSyntax syntax, out TypeSymbol type)
        {
            type = null;
            if (syntax.Parts.Count < 2)
                return false;
            var first = syntax.Parts[0];
            if (Session.NameResolver.ResolveVisibleSymbol(first.Text ?? string.Empty, first.Span) is not ModuleSymbol module)
                return false;
            Symbol current = module;
            for (var index = 1; index < syntax.Parts.Count; index++)
            {
                if (current is not ModuleSymbol currentModule)
                {
                    Session.Diagnostics.ReportUnknownType(syntax.GetSpan(), syntax.GetText());
                    type = TypeSymbol.Error;
                    return true;
                }

                current = Session.NameResolver.LookupModuleMember(currentModule, syntax.Parts[index].Text ?? string.Empty, syntax.Parts[index].Span, out var memberDiagnosticReported);
                if (current == null)
                {
                    if (!memberDiagnosticReported)
                    {
                        Session.Diagnostics.ReportUndefinedMember(syntax.Parts[index].Span, currentModule.QualifiedName, syntax.Parts[index].Text ?? string.Empty);
                    }

                    type = TypeSymbol.Error;
                    return true;
                }
            }

            type = current as TypeSymbol;
            if (type != null)
                return true;
            Session.Diagnostics.ReportUnknownType(syntax.GetSpan(), syntax.GetText());
            type = TypeSymbol.Error;
            return true;
        }

        internal TypeSymbol ResolveCanonicalType(TypeSymbol type)
        {
            if (type?.TypeKind == TypeKind.Array)
                return TypeSymbol.Array(Session.TypeResolver.ResolveCanonicalType(type.ElementType));
            if (type?.TypeKind == TypeKind.Tuple)
            {
                var elements = new TypeSymbol[type.TupleElementTypes.Count];
                for (var index = 0; index < elements.Length; index++)
                    elements[index] = Session.TypeResolver.ResolveCanonicalType(type.TupleElementTypes[index]);
                return TypeSymbol.Tuple(elements);
            }

            if (Session.Environment.ExternCatalog.TryGetTypeSymbol(type.QualifiedName, out var environmentType))
                return environmentType;
            return type;
        }

        internal bool TryResolveTypeNameQuiet(string typeName, TextSpan span, out TypeSymbol type)
        {
            if (string.Equals(typeName, "Self", StringComparison.Ordinal) && Session.Body.CurrentType != null)
            {
                type = Session.Body.CurrentType;
                return true;
            }

            if (TypeResolver.BuiltInTypes.TryGetValue(typeName, out type) || Session.NameResolver.TryGetCurrentModuleType(typeName, out type) || Session.Environment.ExternCatalog.TryGetTypeSymbol(typeName, out type))
            {
                return true;
            }

            var visible = Session.NameResolver.ResolveVisibleSymbol(typeName, span);
            if (visible is TypeSymbol visibleType)
            {
                type = visibleType;
                return true;
            }

            if (EventCatalog.TryGetKnownType(typeName, out var eventType))
            {
                type = Session.TypeResolver.ResolveCanonicalType(eventType);
                return true;
            }

            return false;
        }

        internal bool CanResolveRepeatValueOperand(ExpressionSyntax syntax)
        {
            if (syntax is NameExpressionSyntax name)
            {
                return Session.NameResolver.LookupScopedSymbol(name.Name) is VariableSymbol or ParameterSymbol || Session.Declarations.StateSymbols.ContainsKey(name.Name) || Session.Modules.VisibleConstants.ContainsKey(name.Name) || Session.NameResolver.ResolveVisibleSymbol(name.Name, Session.BinderSyntaxFacts.GetExpressionSpan(name)) is ConstantSymbol;
            }

            if (syntax is ArrayLiteralExpressionSyntax array && !array.IsRepeat && array.Elements.Count == 1 && array.SeparatorTokens.Count == 0)
            {
                return Session.TypeResolver.CanResolveRepeatValueOperand(array.Elements[0]);
            }

            if (syntax is MemberAccessExpressionSyntax member && Session.TypeResolver.TryGetRootName(member, out var rootName) && (Session.NameResolver.LookupScopedSymbol(rootName) != null || Session.Declarations.StateSymbols.ContainsKey(rootName)))
            {
                return true;
            }

            if (syntax is MemberAccessExpressionSyntax qualifiedMember && Session.TypeResolver.TryGetQualifiedName(qualifiedMember, out var qualifiedName) && Session.TypeResolver.TryResolveTypeNameQuiet(qualifiedName, Session.BinderSyntaxFacts.GetExpressionSpan(qualifiedMember), out _))
            {
                return false;
            }

            return syntax is not NameExpressionSyntax;
        }

        internal bool TryGetRootName(MemberAccessExpressionSyntax syntax, out string name)
        {
            ExpressionSyntax current = syntax;
            while (current is MemberAccessExpressionSyntax member)
                current = member.Expression;
            if (current is NameExpressionSyntax root)
            {
                name = root.Name;
                return true;
            }

            name = null;
            return false;
        }

        internal bool TryGetQualifiedName(MemberAccessExpressionSyntax syntax, out string qualifiedName)
        {
            var parts = new List<string>();
            ExpressionSyntax current = syntax;
            while (current is MemberAccessExpressionSyntax member)
            {
                parts.Add(member.MemberName);
                current = member.Expression;
            }

            if (current is not NameExpressionSyntax root)
            {
                qualifiedName = null;
                return false;
            }

            parts.Add(root.Name);
            parts.Reverse();
            qualifiedName = string.Join(".", parts);
            return true;
        }

        internal bool TryGetInt32Constant(BoundExpression expression, out int value)
        {
            if (Session.ConstantEvaluator.TryEvaluateStateConstant(expression, TypeSymbol.I32, out var constant) && constant is int intValue)
            {
                value = intValue;
                return true;
            }

            value = 0;
            return false;
        }
    }
}
