using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Parser;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class BodyBindingPhase : BinderComponent
    {
        internal BodyBindingPhase(BindingSession session) : base(session)
        {
        }

        internal BodyBindingResult Execute(StandardLibraryModuleGraph graph)
        {
            var functions = BindFunctions(graph);
            var events = new List<BoundEventDeclaration>();
            var networkReceivers = new List<BoundNetworkReceiveDeclaration>();
            var declaredEvents = new HashSet<string>(StringComparer.Ordinal);

            foreach (var module in graph.Modules)
            {
                Session.ModuleResolver.SetCurrentModule(module, includeFunctions: true);
                foreach (var member in module.Syntax.Members)
                {
                    if (IsDeclaration(member))
                        continue;

                    if (member is StateDeclarationSyntax)
                    {
                        continue;
                    }

                    if (member is StateBlockDeclarationSyntax stateBlock)
                    {
                        if (!module.IsEntry)
                            Session.Diagnostics.ReportStateBlockOutsideEntry(stateBlock.StateKeyword.Span);
                        continue;
                    }

                    if (member is BehaviorDeclarationSyntax behavior)
                    {
                        if (!module.IsEntry)
                        {
                            Session.Diagnostics.ReportBehaviorOutsideEntry(behavior.BehaviorKeyword.Span);
                            continue;
                        }
                        foreach (var behaviorMember in behavior.Members)
                        {
                            if (behaviorMember is EventDeclarationSyntax behaviorEvent)
                                events.Add(Session.EventDeclarationBinder.Bind(behaviorEvent, declaredEvents));
                            else if (behaviorMember is ReceiveDeclarationSyntax behaviorReceive &&
                                Session.Callables.NetworkReceiveSymbolsBySyntax.TryGetValue(behaviorReceive, out var behaviorReceiveSymbol))
                                networkReceivers.Add(Session.ReceiveDeclarationBinder.Bind(behaviorReceive, behaviorReceiveSymbol));
                        }
                        continue;
                    }

                    if (member is EventDeclarationSyntax)
                    {
                        continue;
                    }

                    if (member is ReceiveDeclarationSyntax)
                    {
                        continue;
                    }

                    if (member is SkippedMemberSyntax skippedMember)
                    {
                        Session.Diagnostics.ReportUnsupportedMember(
                            skippedMember.BadToken.Span,
                            skippedMember.BadToken.Text ?? "");
                        continue;
                    }

                    Session.Diagnostics.ReportUnsupportedMember(
                        module.Syntax.EndOfFileToken.Span,
                        member.GetType().Name);
                }
            }

            BindPendingGenericMethods(functions);
            return new BodyBindingResult(functions, events, networkReceivers);
        }

        private List<BoundFunctionDeclaration> BindFunctions(StandardLibraryModuleGraph graph)
        {
            var functions = new List<BoundFunctionDeclaration>();
            foreach (var module in graph.Modules)
            {
                Session.ModuleResolver.SetCurrentModule(module, includeFunctions: true);
                foreach (var member in module.Syntax.Members)
                {
                    if (member is FunctionDeclarationSyntax functionDeclaration &&
                        Session.Callables.FunctionSymbolsBySyntax.TryGetValue(
                            functionDeclaration,
                            out var functionSymbol))
                    {
                        functions.Add(Session.BodyBinder.BindFunctionDeclaration(
                            functionDeclaration,
                            functionSymbol));
                    }

                    if (member is BehaviorDeclarationSyntax behavior && module.IsEntry)
                    {
                        foreach (var behaviorMember in behavior.Members)
                        {
                            if (behaviorMember is FunctionDeclarationSyntax behaviorFunction &&
                                Session.Callables.BehaviorFunctionSymbolsBySyntax.TryGetValue(behaviorFunction, out var behaviorSymbol))
                                functions.Add(Session.BodyBinder.BindFunctionDeclaration(behaviorFunction, behaviorSymbol));
                        }
                    }

                    if (member is not ImplementationDeclarationSyntax implementationDeclaration)
                        continue;

                    foreach (var methodSyntax in implementationDeclaration.Methods)
                    {
                        if (Session.Callables.MethodSymbolsBySyntax.TryGetValue(methodSyntax, out var methodSymbol))
                            functions.Add(Session.BodyBinder.BindFunctionDeclaration(methodSyntax, methodSymbol));
                    }
                }
            }

            return functions;
        }

        private void BindPendingGenericMethods(List<BoundFunctionDeclaration> functions)
        {
            for (var index = 0; index < Session.Generics.PendingMethodBindings.Count; index++)
            {
                var pending = Session.Generics.PendingMethodBindings[index];
                Session.ModuleResolver.SetCurrentModule(pending.Template.Module, includeFunctions: true);
                var previousGenericParameters = Session.Generics.CurrentTypeParameters;
                var concreteParameters = new Dictionary<string, TypeSymbol>(StringComparer.Ordinal);
                foreach (var parameter in pending.Template.Parameters)
                {
                    if (pending.Substitutions.TryGetValue(parameter, out var concrete))
                        concreteParameters[parameter.Name] = concrete;
                }

                Session.Generics.CurrentTypeParameters = concreteParameters;
                try
                {
                    functions.Add(Session.BodyBinder.BindFunctionDeclaration(
                        pending.Syntax,
                        pending.Function));
                }
                finally
                {
                    Session.Generics.CurrentTypeParameters = previousGenericParameters;
                }
            }
        }

        private static bool IsDeclaration(MemberSyntax member)
        {
            return member is UseDirectiveSyntax or
                ModuleDeclarationSyntax or
                TypeDeclarationSyntax or
                StructDeclarationSyntax or
                EnumDeclarationSyntax or
                FunctionDeclarationSyntax or
                ImplementationDeclarationSyntax or
                ConstDeclarationSyntax or
                LegacyTopLevelLetDeclarationSyntax;
        }
    }
}
