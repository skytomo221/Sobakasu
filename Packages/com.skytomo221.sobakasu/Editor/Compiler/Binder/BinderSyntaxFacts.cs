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
    internal sealed class BinderSyntaxFacts : BinderComponent
    {
        internal BinderSyntaxFacts(BindingSession session) : base(session)
        {
        }

        internal TextSpan GetUseDirectiveSpan(UseDirectiveSyntax syntax)
        {
            var end = syntax.SemicolonToken?.Span.End ?? syntax.UseKeyword.Span.End;
            if (end <= syntax.UseKeyword.Span.Start)
            {
                if (syntax.Alias != null)
                    end = syntax.Alias.Span.End;
                else if (syntax.Path != null && syntax.Path.Identifiers.Count > 0)
                    end = syntax.Path.Identifiers[^1].Span.End;
            }

            return TextSpan.FromBounds(syntax.UseKeyword.Span.Start, end);
        }

        internal TextSpan GetMemberSpan(MemberSyntax member)
        {
            if (member is StructDeclarationSyntax structDeclaration)
            {
                return TextSpan.FromBounds(structDeclaration.PubKeyword?.Span.Start ?? structDeclaration.StructKeyword.Span.Start, structDeclaration.CloseBraceToken.Span.End);
            }

            if (member is EnumDeclarationSyntax enumDeclaration)
            {
                return TextSpan.FromBounds(enumDeclaration.PubKeyword?.Span.Start ?? enumDeclaration.EnumKeyword.Span.Start, enumDeclaration.CloseBraceToken.Span.End);
            }

            if (member is StateDeclarationSyntax state)
            {
                var start = state.PubKeyword?.Span.Start ?? state.SynchronizationModifier?.SyncKeyword.Span.Start ?? state.StateKeyword.Span.Start;
                return TextSpan.FromBounds(start, state.SemicolonToken.Span.End);
            }

            if (member is ConstDeclarationSyntax constant)
            {
                var start = constant.PubKeyword?.Span.Start ?? constant.RejectedSynchronizationModifier?.SyncKeyword.Span.Start ?? constant.ConstKeyword.Span.Start;
                return TextSpan.FromBounds(start, constant.SemicolonToken.Span.End);
            }

            if (member is LegacyTopLevelLetDeclarationSyntax legacy)
            {
                return TextSpan.FromBounds(legacy.FirstToken.Span.Start, legacy.SemicolonToken.Span.End);
            }

            if (member is EventDeclarationSyntax eventDeclaration)
            {
                return TextSpan.FromBounds(eventDeclaration.OnKeyword.Span.Start, eventDeclaration.Body.CloseBraceToken.Span.End);
            }

            return new TextSpan(0, 0);
        }

        internal TextSpan GetFunctionNameSpan(FunctionDeclarationSyntax syntax)
        {
            if (syntax.OperatorToken != null)
            {
                return TextSpan.FromBounds(syntax.AtToken?.Span.Start ?? syntax.OperatorToken.Span.Start, syntax.OperatorToken.Span.End);
            }

            return syntax.QuestionToken == null ? syntax.Identifier.Span : TextSpan.FromBounds(syntax.Identifier.Span.Start, syntax.QuestionToken.Span.End);
        }

        internal TextSpan GetStatementSpan(StatementSyntax syntax)
        {
            if (syntax is SendStatementSyntax sendStatement)
            {
                return TextSpan.FromBounds(sendStatement.SendKeyword.Span.Start, sendStatement.SemicolonToken.Span.End);
            }

            if (syntax is ExpressionStatementSyntax expressionStatement)
            {
                var expressionSpan = Session.BinderSyntaxFacts.GetExpressionSpan(expressionStatement.Expression);
                return TextSpan.FromBounds(expressionSpan.Start, expressionStatement.SemicolonToken?.Span.End ?? expressionSpan.End);
            }

            if (syntax is VariableDeclarationStatementSyntax variableDeclarationStatement)
            {
                return TextSpan.FromBounds(variableDeclarationStatement.LetKeyword.Span.Start, variableDeclarationStatement.SemicolonToken.Span.End);
            }

            if (syntax is InvalidLocalDeclarationStatementSyntax invalidDeclaration)
            {
                return TextSpan.FromBounds(invalidDeclaration.Keyword.Span.Start, invalidDeclaration.SemicolonToken.Span.End);
            }

            if (syntax is ReturnStatementSyntax returnStatement)
            {
                return TextSpan.FromBounds(returnStatement.ReturnKeyword.Span.Start, returnStatement.SemicolonToken.Span.End);
            }

            if (syntax is BreakStatementSyntax breakStatement)
            {
                return TextSpan.FromBounds(breakStatement.BreakKeyword.Span.Start, breakStatement.SemicolonToken.Span.End);
            }

            if (syntax is ContinueStatementSyntax continueStatement)
            {
                return TextSpan.FromBounds(continueStatement.ContinueKeyword.Span.Start, continueStatement.SemicolonToken.Span.End);
            }

            if (syntax is RedoStatementSyntax redoStatement)
            {
                return TextSpan.FromBounds(redoStatement.RedoKeyword.Span.Start, redoStatement.SemicolonToken.Span.End);
            }

            if (syntax is BlockStatementSyntax blockStatement)
            {
                return TextSpan.FromBounds(blockStatement.OpenBraceToken.Span.Start, blockStatement.CloseBraceToken.Span.End);
            }

            return new TextSpan(0, 0);
        }

        internal TextSpan GetExpressionSpan(ExpressionSyntax syntax)
        {
            if (syntax is GenericTypeExpressionSyntax genericTypeExpression)
            {
                return TextSpan.FromBounds(Session.BinderSyntaxFacts.GetExpressionSpan(genericTypeExpression.Target).Start, genericTypeExpression.TypeArgumentList.GreaterToken.Span.End);
            }

            if (syntax is AggregateInitializerExpressionSyntax aggregateInitializer)
            {
                return TextSpan.FromBounds(Session.BinderSyntaxFacts.GetExpressionSpan(aggregateInitializer.Target).Start, aggregateInitializer.CloseBraceToken.Span.End);
            }

            if (syntax is ExternExpressionSyntax externExpression)
            {
                return TextSpan.FromBounds(externExpression.ExternKeyword.Span.Start, Session.BinderSyntaxFacts.GetExpressionSpan(externExpression.Expression).End);
            }

            if (syntax is NewExpressionSyntax newExpression)
            {
                return TextSpan.FromBounds(newExpression.NewKeyword.Span.Start, newExpression.CloseParenToken.Span.End);
            }

            if (syntax is AssignmentExpressionSyntax assignmentExpression)
            {
                var expressionSpan = Session.BinderSyntaxFacts.GetExpressionSpan(assignmentExpression.Expression);
                return TextSpan.FromBounds(Session.BinderSyntaxFacts.GetExpressionSpan(assignmentExpression.Target).Start, expressionSpan.End);
            }

            if (syntax is ParenthesizedExpressionSyntax parenthesizedExpression)
            {
                return TextSpan.FromBounds(parenthesizedExpression.OpenParenToken.Span.Start, parenthesizedExpression.CloseParenToken.Span.End);
            }

            if (syntax is TupleExpressionSyntax tupleExpression)
            {
                return TextSpan.FromBounds(tupleExpression.OpenParenToken.Span.Start, tupleExpression.CloseParenToken.Span.End);
            }

            if (syntax is UnaryExpressionSyntax unaryExpression)
            {
                var operandSpan = Session.BinderSyntaxFacts.GetExpressionSpan(unaryExpression.Operand);
                return TextSpan.FromBounds(unaryExpression.OperatorToken.Span.Start, operandSpan.End);
            }

            if (syntax is BinaryExpressionSyntax binaryExpression)
            {
                var leftSpan = Session.BinderSyntaxFacts.GetExpressionSpan(binaryExpression.Left);
                var rightSpan = Session.BinderSyntaxFacts.GetExpressionSpan(binaryExpression.Right);
                return TextSpan.FromBounds(leftSpan.Start, rightSpan.End);
            }

            if (syntax is IfExpressionSyntax ifExpression)
            {
                var end = ifExpression.ElseExpression == null ? ifExpression.ThenBlock.CloseBraceToken.Span.End : Session.BinderSyntaxFacts.GetExpressionSpan(ifExpression.ElseExpression).End;
                return TextSpan.FromBounds(ifExpression.IfKeyword.Span.Start, end);
            }

            if (syntax is MatchExpressionSyntax matchExpression)
            {
                return TextSpan.FromBounds(matchExpression.MatchKeyword.Span.Start, matchExpression.CloseBraceToken.Span.End);
            }

            if (syntax is BlockExpressionSyntax blockExpression)
            {
                return TextSpan.FromBounds(blockExpression.Block.OpenBraceToken.Span.Start, blockExpression.Block.CloseBraceToken.Span.End);
            }

            if (syntax is WhileExpressionSyntax whileExpression)
            {
                var start = whileExpression.Label?.LabelToken.Span.Start ?? whileExpression.WhileKeyword.Span.Start;
                return TextSpan.FromBounds(start, whileExpression.Body.CloseBraceToken.Span.End);
            }

            if (syntax is LoopExpressionSyntax loopExpression)
            {
                var start = loopExpression.Label?.LabelToken.Span.Start ?? loopExpression.LoopKeyword.Span.Start;
                return TextSpan.FromBounds(start, loopExpression.Body.CloseBraceToken.Span.End);
            }

            if (syntax is StringLiteralExpressionSyntax stringLiteralExpression)
                return stringLiteralExpression.StringToken.Span;
            if (syntax is IntegerLiteralExpressionSyntax integerLiteralExpression)
                return integerLiteralExpression.LiteralToken.Span;
            if (syntax is FloatLiteralExpressionSyntax floatLiteralExpression)
                return floatLiteralExpression.LiteralToken.Span;
            if (syntax is CharacterLiteralExpressionSyntax characterLiteralExpression)
                return characterLiteralExpression.LiteralToken.Span;
            if (syntax is BooleanLiteralExpressionSyntax booleanLiteralExpression)
                return booleanLiteralExpression.LiteralToken.Span;
            if (syntax is ArrayLiteralExpressionSyntax arrayLiteralExpression)
            {
                return TextSpan.FromBounds(arrayLiteralExpression.OpenBracketToken.Span.Start, arrayLiteralExpression.CloseBracketToken.Span.End);
            }

            if (syntax is NameExpressionSyntax nameExpression)
            {
                if (nameExpression.QuestionToken == null)
                    return nameExpression.IdentifierToken.Span;
                return TextSpan.FromBounds(nameExpression.IdentifierToken.Span.Start, nameExpression.QuestionToken.Span.End);
            }

            if (syntax is MemberAccessExpressionSyntax memberAccessExpression)
            {
                var leftSpan = Session.BinderSyntaxFacts.GetExpressionSpan(memberAccessExpression.Expression);
                return TextSpan.FromBounds(leftSpan.Start, memberAccessExpression.QuestionToken?.Span.End ?? memberAccessExpression.Name.Span.End);
            }

            if (syntax is ElementAccessExpressionSyntax elementAccessExpression)
            {
                var receiverSpan = Session.BinderSyntaxFacts.GetExpressionSpan(elementAccessExpression.Expression);
                return TextSpan.FromBounds(receiverSpan.Start, elementAccessExpression.CloseBracketToken.Span.End);
            }

            if (syntax is CallExpressionSyntax callExpression)
            {
                var targetSpan = Session.BinderSyntaxFacts.GetExpressionSpan(callExpression.Target);
                return TextSpan.FromBounds(targetSpan.Start, callExpression.CloseParenToken.Span.End);
            }

            return new TextSpan(0, 0);
        }

        internal TextSpan GetExternalAbiSignatureSpan(ExternalAbiSignatureSyntax syntax)
        {
            return TextSpan.FromBounds(syntax.IsConstructor ? syntax.NewKeyword.Span.Start : Session.BinderSyntaxFacts.GetExpressionSpan(syntax.Target).Start, syntax.CloseParenToken.Span.End);
        }

        internal TextSpan GetPatternSpan(PatternSyntax syntax)
        {
            if (syntax is WildcardPatternSyntax wildcard)
                return wildcard.UnderscoreToken.Span;
            if (syntax is LiteralPatternSyntax literal)
                return literal.LiteralToken.Span;
            if (syntax is UnsupportedPatternSyntax unsupported)
                return unsupported.Token.Span;
            if (syntax is EnumTupleVariantPatternSyntax tuple)
            {
                return TextSpan.FromBounds(tuple.EnumType.GetSpan().Start, tuple.CloseParenToken.Span.End);
            }

            if (syntax is EnumStructVariantPatternSyntax @struct)
            {
                return TextSpan.FromBounds(@struct.EnumType.GetSpan().Start, @struct.CloseBraceToken.Span.End);
            }

            if (syntax is EnumVariantPatternSyntax enumVariant)
            {
                return TextSpan.FromBounds(enumVariant.EnumType.GetSpan().Start, enumVariant.VariantIdentifier.Span.End);
            }

            return new TextSpan(0, 0);
        }

        internal string UnquoteString(string tokenText)
        {
            if (string.IsNullOrEmpty(tokenText))
                return string.Empty;
            if (tokenText.Length < 2)
                return tokenText;
            if (tokenText[0] != '"' || tokenText[^1] != '"')
                return tokenText;
            return tokenText[1..^1];
        }
    }
}
