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
    internal sealed class CallableDeclarationBinder : BinderComponent
    {
        internal CallableDeclarationBinder(BindingSession session) : base(session)
        {
        }

        internal void CollectImplMethodSignatures(ImplDeclarationSyntax syntax)
        {
            if (syntax.GenericParameters != null)
            {
                Session.CallableDeclarationBinder.CollectGenericImplMethodSignatures(syntax);
                return;
            }

            var targetName = syntax.TargetType.GetText();
            TypeSymbol targetType;
            if (syntax.IsExternalBinding)
            {
                if (!Session.Declarations.ExternalTypesBySyntax.TryGetValue(syntax, out targetType))
                    return;
            }
            else
            {
                if (syntax.PubKeyword != null)
                {
                    Session.Diagnostics.ReportPublicModifierNotAllowedOnAdditionalImpl(syntax.PubKeyword.Span);
                }

                targetType = Session.TypeResolver.BindTypeSyntax(syntax.TargetType);
                if (targetType == TypeSymbol.Error)
                {
                    Session.Diagnostics.ReportUnknownImplTarget(syntax.TargetType.GetSpan(), targetName);
                    return;
                }

                if (targetType.IsConstructedGenericType)
                {
                    Session.Diagnostics.ReportInvalidGenericImplTarget(syntax.TargetType.GetSpan(), targetName);
                    return;
                }
            }

            var previousType = Session.Body.CurrentType;
            Session.Body.CurrentType = targetType;
            try
            {
                foreach (var methodSyntax in syntax.Methods)
                    Session.CallableDeclarationBinder.CollectImplMethodSignature(methodSyntax, targetType);
            }
            finally
            {
                Session.Body.CurrentType = previousType;
            }
        }

        internal void CollectImplMethodSignature(FunctionDeclarationSyntax syntax, TypeSymbol targetType)
        {
            var isStatic = syntax.StaticKeyword != null;
            var isOperator = syntax.OperatorToken != null;
            var operatorKind = syntax.OperatorToken?.Kind;
            var genericParameters = Session.CallableDeclarationBinder.CreateFunctionGenericParameters(syntax);
            var previousGenericParameters = Session.Generics.CurrentTypeParameters;
            Session.Generics.CurrentTypeParameters = Session.AggregateDeclarationBinder.CreateGenericParameterScope(genericParameters);
            IReadOnlyList<ParameterSymbol> parameters;
            TypeSymbol returnType;
            try
            {
                parameters = Session.CallableDeclarationBinder.BindMethodParameters(syntax.Parameters);
                returnType = syntax.ReturnTypeAnnotation == null ? syntax.IsExternalBinding ? TypeSymbol.Error : TypeSymbol.Unit : Session.TypeResolver.BindTypeSyntax(syntax.ReturnTypeAnnotation.Type);
            }
            finally
            {
                Session.Generics.CurrentTypeParameters = previousGenericParameters;
            }
            var nameSpan = Session.BinderSyntaxFacts.GetFunctionNameSpan(syntax);
            var selfParameter = isStatic ? null : new ParameterSymbol("self", targetType, -1, "self", nameSpan);
            var symbol = new FunctionSymbol(syntax.Name, returnType, parameters, nameSpan, targetType, selfParameter, isStatic, syntax.PubKeyword != null, isOperator, operatorKind, Session.Modules.CurrentModule?.LogicalName, genericParameters);
            Session.Callables.MethodSymbolsBySyntax[syntax] = symbol;
            if (syntax.IsExternalBinding)
            {
                Session.Generics.CurrentTypeParameters = Session.AggregateDeclarationBinder.CreateGenericParameterScope(genericParameters);
                try
                {
                    Session.ExternDeclarationBinder.BindExternalFunctionSignature(syntax, symbol);
                }
                finally
                {
                    Session.Generics.CurrentTypeParameters = previousGenericParameters;
                }
            }
            if (isOperator)
            {
                Session.CallableDeclarationBinder.ValidateOperatorDeclaration(syntax, targetType, parameters, symbol.ReturnType, nameSpan);
            }

            var methodGroup = Session.CallableDeclarationBinder.GetOrCreateUserMethodGroup(targetType, symbol.Name);
            foreach (var existing in methodGroup.Methods)
            {
                if (Session.CallableDeclarationBinder.HaveSameParameterTypes(existing.Parameters, symbol.Parameters))
                {
                    Session.Diagnostics.ReportDuplicateMethodSignature(nameSpan, symbol.DisplayName);
                    return;
                }
            }

            methodGroup.AddMethod(new UserMethodSymbol(symbol));
        }

        internal IReadOnlyList<ParameterSymbol> BindMethodParameters(IReadOnlyList<ParameterSyntax> parameterSyntaxes)
        {
            var parameters = new List<ParameterSymbol>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var syntax in parameterSyntaxes)
            {
                var name = syntax.Identifier.Text ?? string.Empty;
                if (string.Equals(name, "self", StringComparison.Ordinal))
                {
                    Session.Diagnostics.ReportExplicitSelfParameter(syntax.Identifier.Span);
                    continue;
                }

                if (!names.Add(name))
                    Session.Diagnostics.ReportDuplicateParameterName(syntax.Identifier.Span, name);
                parameters.Add(new ParameterSymbol(name, Session.TypeResolver.BindTypeSyntax(syntax.Type), parameters.Count, name, syntax.Identifier.Span));
            }

            return parameters;
        }

        internal IReadOnlyList<TypeSymbol> CreateFunctionGenericParameters(
            FunctionDeclarationSyntax syntax)
        {
            if (syntax.GenericParameters == null)
                return Array.Empty<TypeSymbol>();
            var result = new List<TypeSymbol>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < syntax.GenericParameters.Parameters.Count; index++)
            {
                var parameterSyntax = syntax.GenericParameters.Parameters[index];
                var name = parameterSyntax.Text ?? string.Empty;
                if (!names.Add(name))
                    Session.Diagnostics.ReportDuplicateGenericParameter(
                        parameterSyntax.Span, syntax.Name, name);
                result.Add(TypeSymbol.CreateGenericParameter(
                    name, syntax, index, syntax.Name));
            }
            return result;
        }

        internal void ValidateOperatorDeclaration(FunctionDeclarationSyntax syntax, TypeSymbol targetType, IReadOnlyList<ParameterSymbol> parameters, TypeSymbol returnType, TextSpan span)
        {
            var kind = syntax.OperatorToken.Kind;
            var isUnary = syntax.AtToken != null;
            if (syntax.StaticKeyword != null)
                Session.Diagnostics.ReportInvalidOperatorName(span, syntax.Name);
            if (isUnary)
            {
                if (kind != SyntaxKind.PlusToken && kind != SyntaxKind.MinusToken && kind != SyntaxKind.BangToken && kind != SyntaxKind.TildeToken)
                {
                    Session.Diagnostics.ReportInvalidOperatorName(span, syntax.Name);
                }

                if (parameters.Count != 0)
                    Session.Diagnostics.ReportInvalidUnaryOperatorArity(span, syntax.Name);
            }
            else
            {
                if (!Session.CallableDeclarationBinder.IsOverloadableBinaryOperator(kind))
                    Session.Diagnostics.ReportOperatorCannotBeOverloaded(span, Session.OperatorResolver.GetOperatorText(kind));
                if (parameters.Count != 1)
                    Session.Diagnostics.ReportInvalidBinaryOperatorArity(span, syntax.Name);
            }

            if (Session.CallableDeclarationBinder.IsComparisonOperator(kind) && returnType != TypeSymbol.Bool)
                Session.Diagnostics.ReportComparisonOperatorMustReturnBool(span, syntax.Name);
        }

        internal bool IsOverloadableBinaryOperator(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.StarToken or SyntaxKind.SlashToken or SyntaxKind.PercentToken or SyntaxKind.EqualsEqualsToken or SyntaxKind.BangEqualsToken or SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or SyntaxKind.GreaterToken or SyntaxKind.GreaterOrEqualsToken or SyntaxKind.AmpersandToken or SyntaxKind.PipeToken or SyntaxKind.CaretToken or SyntaxKind.LessLessToken or SyntaxKind.GreaterGreaterToken => true,
                _ => false,
            };
        }

        internal bool IsComparisonOperator(SyntaxKind kind)
        {
            return kind == SyntaxKind.EqualsEqualsToken || kind == SyntaxKind.BangEqualsToken || kind == SyntaxKind.LessToken || kind == SyntaxKind.LessOrEqualsToken || kind == SyntaxKind.GreaterToken || kind == SyntaxKind.GreaterOrEqualsToken;
        }

        internal MethodGroupSymbol GetOrCreateUserMethodGroup(TypeSymbol type, string name)
        {
            if (!Session.Declarations.MethodGroupsByType.TryGetValue(type, out var groups))
            {
                groups = new Dictionary<string, MethodGroupSymbol>(StringComparer.Ordinal);
                Session.Declarations.MethodGroupsByType.Add(type, groups);
            }

            if (!groups.TryGetValue(name, out var group))
            {
                group = new MethodGroupSymbol(name, type);
                groups.Add(name, group);
            }

            return group;
        }

        internal bool HaveSameParameterTypes(IReadOnlyList<ParameterSymbol> left, IReadOnlyList<ParameterSymbol> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
            {
                if (left[index].Type != right[index].Type)
                    return false;
            }

            return true;
        }

        internal void CollectFunctionSignature(FunctionDeclarationSyntax syntax)
        {
            if (syntax.OperatorToken != null || syntax.StaticKeyword != null)
            {
                Session.Diagnostics.ReportInvalidOperatorName(Session.BinderSyntaxFacts.GetFunctionNameSpan(syntax), syntax.Name);
                return;
            }

            var functionName = syntax.Name;
            var genericParameters = Session.CallableDeclarationBinder.CreateFunctionGenericParameters(syntax);
            var previousGenericParameters = Session.Generics.CurrentTypeParameters;
            Session.Generics.CurrentTypeParameters = Session.AggregateDeclarationBinder.CreateGenericParameterScope(genericParameters);
            IReadOnlyList<ParameterSymbol> parameters;
            TypeSymbol returnType;
            try
            {
                parameters = Session.CallableDeclarationBinder.BindFunctionParameters(syntax.Parameters);
                returnType = syntax.ReturnTypeAnnotation == null ? syntax.IsExternalBinding ? TypeSymbol.Error : TypeSymbol.Unit : Session.TypeResolver.BindTypeSyntax(syntax.ReturnTypeAnnotation.Type);
            }
            finally
            {
                Session.Generics.CurrentTypeParameters = previousGenericParameters;
            }
            var functionNameSpan = Session.BinderSyntaxFacts.GetFunctionNameSpan(syntax);
            if (Session.Modules.VisibleConstants.ContainsKey(functionName))
            {
                Session.Diagnostics.ReportTopLevelDeclarationNameConflict(functionNameSpan, functionName, "constant");
            }

            var functionSymbol = new FunctionSymbol(functionName, returnType, parameters, functionNameSpan, isPublic: syntax.PubKeyword != null, declaringModule: Session.Modules.CurrentModule?.LogicalName, genericParameters: genericParameters);
            Session.Callables.FunctionSymbolsBySyntax[syntax] = functionSymbol;
            if (syntax.IsExternalBinding)
            {
                Session.Generics.CurrentTypeParameters = Session.AggregateDeclarationBinder.CreateGenericParameterScope(genericParameters);
                try
                {
                    Session.ExternDeclarationBinder.BindExternalFunctionSignature(syntax, functionSymbol);
                }
                finally
                {
                    Session.Generics.CurrentTypeParameters = previousGenericParameters;
                }
            }
            if (!Session.Modules.VisibleFunctions.TryGetValue(functionName, out var functionGroup))
            {
                functionGroup = new FunctionGroupSymbol(functionName);
                Session.Modules.VisibleFunctions.Add(functionName, functionGroup);
                Session.CallableDeclarationBinder.RegisterModuleFunctionGroup(functionName, functionGroup);
            }

            foreach (var existing in functionGroup.Functions)
            {
                if (!Session.CallableDeclarationBinder.HaveSameParameterTypes(existing.Parameters, functionSymbol.Parameters))
                    continue;
                Session.Diagnostics.ReportDuplicateFunctionOverload(functionNameSpan, functionSymbol.Signature);
                return;
            }

            functionGroup.AddFunction(functionSymbol);
            if (functionSymbol.IsPublic)
                Session.CallableDeclarationBinder.RegisterPublicFunctionOverload(functionSymbol);
        }

        internal void RegisterModuleFunctionGroup(string name, FunctionGroupSymbol functionGroup)
        {
            if (Session.Modules.CurrentModule == null || !Session.Modules.Symbols.TryGetValue(Session.Modules.CurrentModule, out var moduleSymbol))
            {
                return;
            }

            moduleSymbol.TryDeclare(name, functionGroup);
        }

        internal void RegisterPublicFunctionOverload(FunctionSymbol function)
        {
            if (Session.Modules.CurrentModule == null || !Session.Modules.Symbols.TryGetValue(Session.Modules.CurrentModule, out var moduleSymbol))
            {
                return;
            }

            var exported = moduleSymbol.LookupExport(function.Name);
            FunctionGroupSymbol publicGroup;
            if (exported is FunctionGroupSymbol existingGroup)
            {
                publicGroup = existingGroup;
            }
            else
            {
                publicGroup = new FunctionGroupSymbol(function.Name);
                if (!moduleSymbol.TryExport(function.Name, publicGroup, out _))
                    return;
            }

            publicGroup.AddFunction(function);
            if (!string.IsNullOrEmpty(moduleSymbol.CanonicalPublicPath))
            {
                function.RegisterPublicPath($"{moduleSymbol.CanonicalPublicPath}.{function.Name}");
            }
        }

        internal void RegisterModuleDeclaration(string name, Symbol symbol, bool isPublic)
        {
            if (Session.Modules.CurrentModule == null || !Session.Modules.Symbols.TryGetValue(Session.Modules.CurrentModule, out var moduleSymbol))
            {
                return;
            }

            moduleSymbol.TryDeclare(name, symbol);
            if (!isPublic)
                return;
            moduleSymbol.TryExport(name, symbol, out _);
            if (!string.IsNullOrEmpty(moduleSymbol.CanonicalPublicPath))
            {
                Session.ImportResolver.RegisterCanonicalPublicPath(symbol, $"{moduleSymbol.CanonicalPublicPath}.{name}");
            }
        }

        internal void CollectGenericImplMethodSignatures(ImplDeclarationSyntax syntax)
        {
            if (syntax.IsExternalBinding || syntax.PubKeyword != null)
            {
                Session.Diagnostics.ReportInvalidGenericImplTarget(syntax.TargetType.GetSpan(), syntax.TargetType.GetText());
                return;
            }

            var implParameters = new List<TypeSymbol>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < syntax.GenericParameters.Parameters.Count; index++)
            {
                var parameterSyntax = syntax.GenericParameters.Parameters[index];
                var name = parameterSyntax.Text ?? string.Empty;
                if (!names.Add(name))
                {
                    Session.Diagnostics.ReportDuplicateGenericParameter(parameterSyntax.Span, syntax.TargetType.GetText(), name);
                }

                implParameters.Add(TypeSymbol.CreateGenericParameter(name, syntax, index, $"impl {syntax.TargetType.GetNameText()}"));
            }

            var previousGenericParameters = Session.Generics.CurrentTypeParameters;
            Session.Generics.CurrentTypeParameters = Session.AggregateDeclarationBinder.CreateGenericParameterScope(implParameters);
            try
            {
                var openTarget = Session.TypeResolver.BindTypeSyntax(syntax.TargetType);
                if (!Session.CallableDeclarationBinder.IsValidGenericImplTarget(openTarget, implParameters))
                {
                    Session.Diagnostics.ReportInvalidGenericImplTarget(syntax.TargetType.GetSpan(), syntax.TargetType.GetText());
                    return;
                }

                var template = new GenericImplTemplate(openTarget.GenericDefinition, openTarget, implParameters, Session.Modules.CurrentModule);
                foreach (var methodSyntax in syntax.Methods)
                {
                    var parameters = Session.CallableDeclarationBinder.BindMethodParameters(methodSyntax.Parameters);
                    var returnType = methodSyntax.ReturnTypeAnnotation == null ? methodSyntax.IsExternalBinding ? TypeSymbol.Error : TypeSymbol.Unit : Session.TypeResolver.BindTypeSyntax(methodSyntax.ReturnTypeAnnotation.Type);
                    var nameSpan = Session.BinderSyntaxFacts.GetFunctionNameSpan(methodSyntax);
                    var isStatic = methodSyntax.StaticKeyword != null;
                    var openFunction = new FunctionSymbol(methodSyntax.Name, returnType, parameters, nameSpan, openTarget, isStatic ? null : new ParameterSymbol("self", openTarget, -1, "self", nameSpan), isStatic, methodSyntax.PubKeyword != null, methodSyntax.OperatorToken != null, methodSyntax.OperatorToken?.Kind, Session.Modules.CurrentModule?.LogicalName);
                    template.Methods.Add(new GenericMethodTemplate(methodSyntax, openFunction));
                    Session.Callables.FunctionModulesBySyntax[methodSyntax] = Session.Modules.CurrentModule;
                }

                if (!Session.Generics.ImplTemplates.TryGetValue(openTarget.GenericDefinition, out var templates))
                {
                    templates = new List<GenericImplTemplate>();
                    Session.Generics.ImplTemplates.Add(openTarget.GenericDefinition, templates);
                }

                templates.Add(template);
            }
            finally
            {
                Session.Generics.CurrentTypeParameters = previousGenericParameters;
            }
        }

        internal bool IsValidGenericImplTarget(TypeSymbol target, IReadOnlyList<TypeSymbol> parameters)
        {
            if (target?.IsConstructedGenericType != true || target.GenericDefinition?.IsAggregate != true || target.TypeArguments.Count != parameters.Count)
            {
                return false;
            }

            var allowed = new HashSet<TypeSymbol>(parameters);
            var seen = new HashSet<TypeSymbol>();
            foreach (var argument in target.TypeArguments)
            {
                if (!argument.IsGenericParameter || !allowed.Contains(argument) || !seen.Add(argument))
                {
                    return false;
                }
            }

            return seen.Count == parameters.Count;
        }

        internal IReadOnlyList<ParameterSymbol> BindFunctionParameters(IReadOnlyList<ParameterSyntax> parameterSyntaxes)
        {
            var parameters = new List<ParameterSymbol>();
            var seenParameterNames = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < parameterSyntaxes.Count; index++)
            {
                var parameterSyntax = parameterSyntaxes[index];
                var parameterName = parameterSyntax.Identifier.Text ?? string.Empty;
                if (!seenParameterNames.Add(parameterName))
                    Session.Diagnostics.ReportDuplicateParameterName(parameterSyntax.Identifier.Span, parameterName);
                var parameterType = Session.TypeResolver.BindTypeSyntax(parameterSyntax.Type);
                parameters.Add(new ParameterSymbol(parameterName, parameterType, index, parameterName, parameterSyntax.Identifier.Span));
            }

            return parameters;
        }

        internal void CollectNetworkReceiveSignatures(IReadOnlyList<MemberSyntax> members)
        {
            var receiverOrdinal = 0;
            foreach (var member in members)
            {
                if (member is not ReceiveDeclarationSyntax syntax)
                    continue;
                var name = syntax.Identifier.Text ?? string.Empty;
                var parameters = Session.CallableDeclarationBinder.BindFunctionParameters(syntax.Parameters);
                var physicalParameters = new List<NetworkReceivePhysicalParameter>();
                foreach (var parameter in parameters)
                {
                    if (parameter.Type == TypeSymbol.Error)
                        continue;
                    IReadOnlyList<AggregateLeafDescriptor> leaves;
                    if (parameter.Type.UsesFlattenedAggregateStorage && parameter.Type.AggregateKind == UserAggregateKind.Struct)
                    {
                        leaves = AggregateLayout.GetLeaves(parameter.Type);
                    }
                    else if (parameter.Type.UsesFlattenedAggregateStorage || parameter.Type.TypeKind == TypeKind.Array && parameter.Type.ElementType?.UsesFlattenedAggregateStorage == true)
                    {
                        Session.Diagnostics.ReportUnsupportedNetworkAggregate(parameter.DeclarationSpan ?? syntax.Identifier.Span, parameter.Type.Name);
                        continue;
                    }
                    else
                    {
                        leaves = new[]
                        {
              new AggregateLeafDescriptor(parameter.Type, Array.Empty<string>())
            };
                    }

                    foreach (var leaf in leaves)
                    {
                        var path = leaf.Path.Count == 0 ? parameter.Name : $"{parameter.Name}.{leaf.PathText}";
                        if (!StateSynchronizationCompatibility.IsSupported(leaf.Type, StateSynchronizationMode.None))
                        {
                            Session.Diagnostics.ReportUnsupportedNetworkParameter(parameter.DeclarationSpan ?? syntax.Identifier.Span, name, path, leaf.Type.Name);
                            continue;
                        }

                        var physicalOrdinal = physicalParameters.Count;
                        var physical = new ParameterSymbol(path.Replace('.', '_'), leaf.Type, physicalOrdinal, $"__receive_param_{receiverOrdinal}_{physicalOrdinal}", parameter.DeclarationSpan);
                        physicalParameters.Add(new NetworkReceivePhysicalParameter(parameter, physical, leaf.Path));
                    }
                }

                if (physicalParameters.Count > 8)
                {
                    Session.Diagnostics.ReportNetworkPhysicalParameterLimit(syntax.Identifier.Span, name, physicalParameters.Count);
                }

                var symbol = new NetworkReceiveSymbol(name, name, parameters, physicalParameters, syntax.Identifier.Span);
                Session.Callables.NetworkReceiveSymbolsBySyntax[syntax] = symbol;
                if (!Session.Callables.NetworkReceiveSymbols.TryAdd(name, symbol))
                {
                    Session.Diagnostics.ReportDuplicateNetworkReceiver(syntax.Identifier.Span, name);
                }
                else if (!Session.Callables.NetworkEntrypointNames.Add(symbol.ExportName))
                {
                    Session.Diagnostics.ReportNetworkEntrypointCollision(syntax.Identifier.Span, symbol.ExportName);
                }

                receiverOrdinal++;
            }
        }
    }
}
