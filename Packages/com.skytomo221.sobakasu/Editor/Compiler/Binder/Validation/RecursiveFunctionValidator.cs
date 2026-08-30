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
  internal sealed class RecursiveFunctionValidator : BinderComponent
  {
    internal RecursiveFunctionValidator(BindingSession session) : base(session)
    {
    }
  
    internal void ReportRecursiveFunctions(IReadOnlyList<BoundFunctionDeclaration> functions)
    {
      var declarations = new Dictionary<FunctionSymbol, BoundFunctionDeclaration>();
      var graph = new Dictionary<FunctionSymbol, HashSet<FunctionSymbol>>();
      foreach (var function in functions)
      {
        declarations[function.FunctionSymbol] = function;
        var callees = new HashSet<FunctionSymbol>();
        Session.RecursiveFunctionValidator.CollectFunctionCallees(function.Body, callees);
        graph[function.FunctionSymbol] = callees;
      }
  
      var states = new Dictionary<FunctionSymbol, int>();
      var stack = new List<FunctionSymbol>();
      var reported = new HashSet<FunctionSymbol>();
      foreach (var function in functions)
        Session.RecursiveFunctionValidator.VisitFunctionForRecursion(function.FunctionSymbol, declarations, graph, states, stack, reported);
    }
  
    internal void VisitFunctionForRecursion(FunctionSymbol function, IReadOnlyDictionary<FunctionSymbol, BoundFunctionDeclaration> declarations, IReadOnlyDictionary<FunctionSymbol, HashSet<FunctionSymbol>> graph, IDictionary<FunctionSymbol, int> states, IList<FunctionSymbol> stack, ISet<FunctionSymbol> reported)
    {
      if (states.TryGetValue(function, out var state))
      {
        if (state == 2)
          return;
      }
  
      states[function] = 1;
      stack.Add(function);
      if (graph.TryGetValue(function, out var callees))
      {
        foreach (var callee in callees)
        {
          if (!declarations.ContainsKey(callee))
            continue;
          if (!states.TryGetValue(callee, out var calleeState))
          {
            Session.RecursiveFunctionValidator.VisitFunctionForRecursion(callee, declarations, graph, states, stack, reported);
            continue;
          }
  
          if (calleeState == 1)
            Session.RecursiveFunctionValidator.ReportFunctionCycle(callee, stack, reported);
        }
      }
  
      stack.RemoveAt(stack.Count - 1);
      states[function] = 2;
    }
  
    internal void ReportFunctionCycle(FunctionSymbol cycleStart, IList<FunctionSymbol> stack, ISet<FunctionSymbol> reported)
    {
      var startIndex = -1;
      for (var index = 0; index < stack.Count; index++)
      {
        if (ReferenceEquals(stack[index], cycleStart))
        {
          startIndex = index;
          break;
        }
      }
  
      if (startIndex < 0)
        return;
      var cycleNames = new List<string>();
      for (var index = startIndex; index < stack.Count; index++)
        cycleNames.Add(stack[index].Name);
      cycleNames.Add(cycleStart.Name);
      var cycleDisplay = string.Join(" -> ", cycleNames);
      for (var index = startIndex; index < stack.Count; index++)
      {
        var function = stack[index];
        if (!reported.Add(function))
          continue;
        var previousSourcePath = Session.Diagnostics.SourcePath;
        if (Session.Callables.ModulesByFunctionSymbol.TryGetValue(function, out var module))
          Session.Diagnostics.SourcePath = module.SourcePath;
        Session.Diagnostics.ReportRecursiveFunction(function.SourceSpan, function.Name, cycleDisplay);
        Session.Diagnostics.SourcePath = previousSourcePath;
      }
    }
  
    internal void CollectFunctionCallees(BoundStatement statement, ISet<FunctionSymbol> callees)
    {
      switch (statement)
      {
        case BoundBlockStatement blockStatement:
          foreach (var child in blockStatement.Statements)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(child, callees);
          return;
        case BoundVariableDeclarationStatement variableDeclaration:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(variableDeclaration.Initializer, callees);
          return;
        case BoundExpressionStatement expressionStatement:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(expressionStatement.Expression, callees);
          return;
        case BoundNetworkSendStatement sendStatement:
          foreach (var argument in sendStatement.Arguments)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(argument, callees);
          Session.RecursiveFunctionValidator.CollectFunctionCallees(sendStatement.Target, callees);
          return;
        case BoundReturnStatement returnStatement:
          if (returnStatement.Expression != null)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(returnStatement.Expression, callees);
          return;
        case BoundBreakStatement breakStatement:
          if (breakStatement.Expression != null)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(breakStatement.Expression, callees);
          return;
      }
    }
  
    internal void CollectFunctionCallees(BoundExpression expression, ISet<FunctionSymbol> callees)
    {
      switch (expression)
      {
        case BoundUserFunctionCallExpression functionCall:
          callees.Add(functionCall.Function);
          if (functionCall.Receiver != null)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(functionCall.Receiver, callees);
          foreach (var argument in functionCall.Arguments)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(argument, callees);
          return;
        case BoundCallExpression callExpression:
          if (callExpression.Target != null)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(callExpression.Target, callees);
          foreach (var argument in callExpression.Arguments)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(argument, callees);
          return;
        case BoundMaybeExternBindingExpression maybeExternBinding:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(maybeExternBinding.RawExpression, callees);
          return;
        case BoundUnaryExpression unaryExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(unaryExpression.Operand, callees);
          return;
        case BoundBinaryExpression binaryExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(binaryExpression.Left, callees);
          Session.RecursiveFunctionValidator.CollectFunctionCallees(binaryExpression.Right, callees);
          return;
        case BoundAssignmentExpression assignmentExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(assignmentExpression.Expression, callees);
          return;
        case BoundAggregateFieldAssignmentExpression fieldAssignment:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(fieldAssignment.Target.Receiver, callees);
          Session.RecursiveFunctionValidator.CollectFunctionCallees(fieldAssignment.Value, callees);
          return;
        case BoundAggregateFieldAccessExpression fieldAccess:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(fieldAccess.Receiver, callees);
          return;
        case BoundStructConstructionExpression structConstruction:
          foreach (var initializer in structConstruction.Initializers)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(initializer.Expression, callees);
          return;
        case BoundTupleExpression tupleExpression:
          foreach (var element in tupleExpression.Elements)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(element, callees);
          return;
        case BoundEnumConstructionExpression enumConstruction:
          foreach (var initializer in enumConstruction.Initializers)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(initializer.Expression, callees);
          return;
        case BoundArrayLiteralExpression arrayLiteralExpression:
          foreach (var element in arrayLiteralExpression.Elements)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(element, callees);
          return;
        case BoundArrayRepeatExpression arrayRepeatExpression:
          if (arrayRepeatExpression.Operand != null)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(arrayRepeatExpression.Operand, callees);
          Session.RecursiveFunctionValidator.CollectFunctionCallees(arrayRepeatExpression.Length, callees);
          return;
        case BoundElementAccessExpression elementAccessExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(elementAccessExpression.Array, callees);
          Session.RecursiveFunctionValidator.CollectFunctionCallees(elementAccessExpression.Index, callees);
          return;
        case BoundElementAssignmentExpression elementAssignmentExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(elementAssignmentExpression.Target.Array, callees);
          Session.RecursiveFunctionValidator.CollectFunctionCallees(elementAssignmentExpression.Target.Index, callees);
          Session.RecursiveFunctionValidator.CollectFunctionCallees(elementAssignmentExpression.Value, callees);
          return;
        case BoundArrayLengthExpression arrayLengthExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(arrayLengthExpression.Array, callees);
          return;
        case BoundMemberAccessExpression memberAccessExpression:
          if (memberAccessExpression.Receiver != null)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(memberAccessExpression.Receiver, callees);
          return;
        case BoundBlockExpression blockExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(blockExpression.Block, callees);
          if (blockExpression.TrailingExpression != null)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(blockExpression.TrailingExpression, callees);
          return;
        case BoundIfExpression ifExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(ifExpression.Condition, callees);
          Session.RecursiveFunctionValidator.CollectFunctionCallees(ifExpression.ThenExpression, callees);
          if (ifExpression.ElseExpression != null)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(ifExpression.ElseExpression, callees);
          return;
        case BoundMatchExpression matchExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(matchExpression.Expression, callees);
          foreach (var arm in matchExpression.Arms)
            Session.RecursiveFunctionValidator.CollectFunctionCallees(arm.Expression, callees);
          return;
        case BoundWhileExpression whileExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(whileExpression.Condition, callees);
          Session.RecursiveFunctionValidator.CollectFunctionCallees(whileExpression.Body, callees);
          return;
        case BoundLoopExpression loopExpression:
          Session.RecursiveFunctionValidator.CollectFunctionCallees(loopExpression.Body, callees);
          return;
      }
    }
  }
}
