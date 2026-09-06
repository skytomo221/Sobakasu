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
    internal sealed class NameResolver : BinderComponent
    {
        internal NameResolver(BindingSession session) : base(session)
        {
        }

        internal Symbol LookupModuleMember(ModuleSymbol module, string memberName, TextSpan span, out bool diagnosticReported)
        {
            diagnosticReported = false;
            var exported = module.LookupExport(memberName);
            if (exported != null)
                return exported;
            var declared = module.LookupDeclared(memberName);
            if (declared == null)
                return null;
            Session.Diagnostics.SourcePath = Session.Modules.CurrentModule?.SourcePath ?? string.Empty;
            diagnosticReported = true;
            if (declared is ModuleSymbol childModule)
            {
                if (Session.Modules.CurrentModule != null && ReferenceEquals(childModule.SourceModule.Parent, Session.Modules.CurrentModule))
                {
                    return childModule;
                }

                Session.Diagnostics.ReportModuleNotPublic(span, childModule.QualifiedName);
                return null;
            }

            Session.Diagnostics.ReportModuleMemberNotPublic(span, module.QualifiedName, memberName);
            return null;
        }

        internal LocalVariableSymbol LookupLocal(string name)
        {
            return Session.Body.Scope != null && Session.Body.Scope.TryLookupLocal(name, out var local) ? local : null;
        }

        internal Symbol LookupScopedSymbol(string name)
        {
            return Session.Body.Scope != null && Session.Body.Scope.TryLookupSymbol(name, out var symbol) ? symbol : null;
        }

        internal Symbol ResolveVisibleSymbol(string name, TextSpan span)
        {
            return Session.NameResolver.ResolveVisibleSymbol(name, span, out _);
        }

        internal bool TryResolveImportedEnumVariant(NameExpressionSyntax syntax, out EnumVariantSymbol variant)
        {
            variant = null;
            var name = syntax.Name;
            if (Session.NameResolver.LookupScopedSymbol(name) != null || Session.NameResolver.TryGetCurrentModuleFunctionGroup(name, out _) || ((Session.Modules.CurrentModule == null || Session.Modules.CurrentModule.IsEntry) && Session.Declarations.StateSymbols.ContainsKey(name)))
            {
                return false;
            }

            variant = Session.NameResolver.ResolveVisibleSymbol(name, Session.BinderSyntaxFacts.GetExpressionSpan(syntax)) as EnumVariantSymbol;
            return variant != null;
        }

        internal Symbol ResolveVisibleSymbol(string name, TextSpan span, out bool resolutionHadDiagnostic)
        {
            resolutionHadDiagnostic = false;
            if (Session.Modules.VisibleConstants.TryGetValue(name, out var constantSymbol))
                return constantSymbol;
            if (Session.NameResolver.TryGetCurrentModuleType(name, out var declaredType))
                return declaredType;
            if (Session.Modules.CurrentModule != null && Session.Modules.Symbols.TryGetValue(Session.Modules.CurrentModule, out var currentModuleSymbol) && currentModuleSymbol.Children.TryGetValue(name, out var childModule))
            {
                return childModule;
            }

            if (Session.Modules.CurrentModule != null && Session.Modules.Aliases.TryGetValue(Session.Modules.CurrentModule, out var aliases) && aliases.TryGetValue(name, out var aliasSymbol))
            {
                return aliasSymbol;
            }

            if (Session.Modules.CurrentModule != null && Session.Modules.Imports.TryGetValue(Session.Modules.CurrentModule, out var imports) && imports.TryGetValue(name, out var importedSymbol))
            {
                return importedSymbol;
            }

            if (Session.Modules.CurrentModule != null && Session.Modules.PreludeImports.TryGetValue(Session.Modules.CurrentModule, out var preludeImports) && preludeImports.TryGetValue(name, out var preludeSymbol))
            {
                return preludeSymbol;
            }

            return null;
        }

        internal bool TryGetCurrentModuleType(string name, out TypeSymbol type)
        {
            type = null;
            return Session.Modules.CurrentModule != null && Session.Modules.Types.TryGetValue(Session.Modules.CurrentModule, out var types) && types.TryGetValue(name, out type);
        }

        internal bool TryGetCurrentModuleFunctionGroup(string name, out FunctionGroupSymbol functions)
        {
            functions = null;
            return Session.Modules.CurrentModule != null && Session.Modules.Functions.TryGetValue(Session.Modules.CurrentModule, out var moduleFunctions) && moduleFunctions.TryGetValue(name, out functions);
        }

        internal bool IsExternCallableSymbol(Symbol symbol)
        {
            return symbol is MethodGroupSymbol || symbol is MethodSymbol;
        }

        internal string GetSymbolDisplayName(Symbol symbol)
        {
            if (symbol is NamespaceSymbol namespaceSymbol)
                return namespaceSymbol.QualifiedName;
            if (symbol is ModuleSymbol moduleSymbol)
                return moduleSymbol.QualifiedName;
            if (symbol is TypeSymbol typeSymbol)
                return typeSymbol.QualifiedName;
            if (symbol is EnumVariantSymbol enumVariantSymbol)
                return enumVariantSymbol.DeclarationIdentity;
            if (symbol is MethodGroupSymbol methodGroup)
                return methodGroup.DisplayName;
            if (symbol is MethodSymbol method)
                return method.DisplayName;
            if (symbol is FunctionGroupSymbol functionGroup)
                return functionGroup.Name;
            return symbol?.Name ?? "<unknown>";
        }

        internal Symbol GetReferencedSymbol(BoundExpression expression)
        {
            if (expression is BoundNameExpression nameExpression)
                return nameExpression.Symbol;
            if (expression is BoundMemberAccessExpression memberAccessExpression)
                return memberAccessExpression.MemberSymbol;
            return null;
        }

        internal TypeSymbol GetExpressionType(Symbol symbol)
        {
            if (symbol is TypeSymbol typeSymbol)
                return typeSymbol;
            if (symbol is NamespaceSymbol)
                return TypeSymbol.NamespacePseudoType;
            if (symbol is ModuleSymbol)
                return TypeSymbol.ModulePseudoType;
            if (symbol is ParameterSymbol parameterSymbol)
                return parameterSymbol.Type;
            if (symbol is VariableSymbol variableSymbol)
                return variableSymbol.Type;
            if (symbol is AggregateFieldSymbol aggregateField)
                return aggregateField.Type;
            if (symbol is EnumVariantSymbol enumVariant)
                return enumVariant.ContainingType;
            if (symbol is MethodGroupSymbol || symbol is MethodSymbol)
                return TypeSymbol.MethodGroupPseudoType;
            if (symbol is FunctionGroupSymbol || symbol is FunctionSymbol)
                return TypeSymbol.MethodGroupPseudoType;
            if (symbol is ConstantSymbol constantSymbol)
                return constantSymbol.Type;
            return TypeSymbol.Error;
        }

        internal string GetReceiverDisplayName(BoundExpression receiver)
        {
            var symbol = Session.NameResolver.GetReferencedSymbol(receiver);
            if (symbol is NamespaceSymbol namespaceSymbol)
                return namespaceSymbol.Name;
            if (symbol is ModuleSymbol moduleSymbol)
                return moduleSymbol.QualifiedName;
            if (symbol is TypeSymbol typeSymbol)
                return typeSymbol.Name;
            return receiver.Type.Name;
        }

        internal string GetCallTargetDisplayName(BoundExpression target)
        {
            var symbol = Session.NameResolver.GetReferencedSymbol(target);
            if (symbol is MethodSymbol methodSymbol)
                return methodSymbol.DisplayName;
            if (symbol != null)
                return symbol.Name;
            return target.Type.Name;
        }

        internal bool ContainsError(IReadOnlyList<BoundExpression> arguments)
        {
            foreach (var argument in arguments)
            {
                if (argument.Type == TypeSymbol.Error)
                    return true;
            }

            return false;
        }
    }
}
