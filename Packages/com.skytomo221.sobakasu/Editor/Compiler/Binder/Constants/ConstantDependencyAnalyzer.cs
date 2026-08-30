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
  internal sealed class ConstantDependencyAnalyzer : BinderComponent
  {
    internal ConstantDependencyAnalyzer(BindingSession session) : base(session)
    {
    }
  
    internal IReadOnlyList<BoundConstantDeclaration> BindConstantDeclarations()
    {
      var constants = new List<BoundConstantDeclaration>(Session.Constants.DeclarationOrder.Count);
      foreach (var symbol in Session.Constants.DeclarationOrder)
      {
        var declaration = Session.ConstantDependencyAnalyzer.EnsureConstantBound(symbol, symbol.DeclarationSpan);
        if (declaration != null)
          constants.Add(declaration);
      }
  
      return constants;
    }
  
    internal BoundConstantDeclaration EnsureConstantBound(ConstantSymbol symbol, TextSpan referenceSpan)
    {
      if (Session.Constants.BoundConstants.TryGetValue(symbol, out var existing))
        return existing;
      if (Session.Constants.BindingStates[symbol] == ConstantBindingState.Binding)
      {
        var cycleStart = Session.Constants.BindingStack.IndexOf(symbol);
        var path = new List<string>();
        if (cycleStart >= 0)
        {
          for (var index = cycleStart; index < Session.Constants.BindingStack.Count; index++)
            path.Add(Session.Constants.BindingStack[index].DeclarationIdentity);
        }
  
        path.Add(symbol.DeclarationIdentity);
        Session.Diagnostics.ReportConstantDependencyCycle(referenceSpan, string.Join(" -> ", path));
        symbol.SetBinding(TypeSymbol.Error, null, false, referenceSpan);
        return null;
      }
  
      Session.Constants.BindingStates[symbol] = ConstantBindingState.Binding;
      Session.Constants.BindingStack.Add(symbol);
      var previousModule = Session.Modules.CurrentModule;
      var syntax = Session.Constants.SyntaxBySymbol[symbol];
      try
      {
        Session.ModuleResolver.SetCurrentModule(Session.Constants.ModulesBySymbol[symbol], includeFunctions: true);
        var declaredType = syntax.TypeClause == null ? null : Session.TypeResolver.BindTypeClause(syntax.TypeClause);
        var initializer = syntax.Initializer == null ? BoundErrorExpression.Instance : Session.ExpressionBinder.BindExpression(syntax.Initializer, declaredType);
        var constantType = declaredType;
        if (constantType == null)
        {
          if (initializer.Type == TypeSymbol.Error)
          {
            Session.Diagnostics.ReportCannotInferConstantType(syntax.Identifier.Span, symbol.Name);
            constantType = TypeSymbol.Error;
          }
          else
          {
            constantType = initializer.Type;
          }
        }
        else if (initializer.Type != TypeSymbol.Error && !Session.ConversionClassifier.CanAssignToLocal(constantType, initializer.Type))
        {
          Session.Diagnostics.ReportTypeMismatch(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Initializer), constantType.Name, initializer.Type.Name);
        }
  
        object value = null;
        var hasConstantValue = false;
        if (constantType != TypeSymbol.Error && !Session.ConstantDeclarationBinder.IsSupportedConstantType(constantType))
        {
          Session.Diagnostics.ReportUnsupportedConstantType(syntax.Identifier.Span, symbol.Name, constantType.Name);
        }
        else if (constantType != TypeSymbol.Error)
        {
          hasConstantValue = initializer.Type != TypeSymbol.Error && Session.ConstantEvaluator.TryEvaluateStateConstant(initializer, constantType, out value);
          if (!hasConstantValue)
          {
            Session.Diagnostics.ReportConstantInitializerMustBeConstant(Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Initializer), symbol.Name);
          }
        }
  
        var initializerSpan = syntax.Initializer == null ? syntax.Identifier.Span : Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Initializer);
        symbol.SetBinding(hasConstantValue ? constantType : TypeSymbol.Error, value, hasConstantValue, initializerSpan);
        var declaration = new BoundConstantDeclaration(symbol, initializer);
        Session.Constants.BoundConstants[symbol] = declaration;
        return declaration;
      }
      finally
      {
        Session.Constants.BindingStack.RemoveAt(Session.Constants.BindingStack.Count - 1);
        Session.Constants.BindingStates[symbol] = ConstantBindingState.Bound;
        Session.ModuleResolver.SetCurrentModule(previousModule, includeFunctions: true);
      }
    }
  }
}
