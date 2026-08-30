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
  internal sealed class StatementBinder : BinderComponent
  {
    internal StatementBinder(BindingSession session) : base(session)
    {
    }
  
    internal BoundStatement BindStatement(StatementSyntax syntax)
    {
      if (syntax is SendStatementSyntax sendStatement)
        return Session.NetworkSendBinder.BindNetworkSendStatement(sendStatement);
      if (syntax is VariableDeclarationStatementSyntax variableDeclarationStatement)
        return Session.LocalDeclarationBinder.BindVariableDeclarationStatement(variableDeclarationStatement);
      if (syntax is ReturnStatementSyntax returnStatement)
        return Session.ReturnBinder.BindReturnStatement(returnStatement);
      if (syntax is BreakStatementSyntax breakStatement)
        return Session.LoopBinder.BindBreakStatement(breakStatement);
      if (syntax is ContinueStatementSyntax continueStatement)
        return Session.LoopBinder.BindContinueStatement(continueStatement);
      if (syntax is RedoStatementSyntax redoStatement)
        return Session.LoopBinder.BindRedoStatement(redoStatement);
      if (syntax is ExpressionStatementSyntax expressionStatement)
      {
        return new BoundExpressionStatement(Session.ExpressionBinder.BindExpression(expressionStatement.Expression));
      }
  
      if (syntax is BlockStatementSyntax blockStatement)
        return Session.BlockBinder.BindBlockStatement(blockStatement);
      Session.Diagnostics.ReportUnsupportedStatement(Session.BinderSyntaxFacts.GetStatementSpan(syntax), syntax.GetType().Name);
      return new BoundExpressionStatement(BoundErrorExpression.Instance);
    }
  }
}
