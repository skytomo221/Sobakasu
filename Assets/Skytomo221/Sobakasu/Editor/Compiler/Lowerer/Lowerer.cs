using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{
public sealed class Lowerer
  {
    private int _labelId = 0;

    public IrProgram Lower(BoundProgram program)
    {
      var funcs = new List<IrFunction>();

      foreach (var fn in program.Functions)
      {
        if (!program.Bodies.TryGetValue(fn, out var body))
          continue;

        var lowered = LowerFunctionBody(body);
        funcs.Add(new IrFunction(fn, lowered));
      }

      return new IrProgram(funcs);
    }

    private IrBlock LowerFunctionBody(BoundBlockStatement body)
    {
      var stmts = new List<IrStatement>(256);
      LowerStatement(body, stmts);

      // Udon向け：末尾に return を保証（void）
      if (stmts.Count == 0 || stmts[^1] is not IrReturn)
        stmts.Add(new IrReturn(null));

      return new IrBlock(stmts);
    }

    private void LowerStatement(BoundStatement stmt, List<IrStatement> outStmts)
    {
      switch (stmt)
      {
        case BoundBlockStatement block:
          foreach (var s in block.Statements)
            LowerStatement(s, outStmts);
          break;

        case BoundLetStatement let:
          // let は CodeGen では「ローカル確保＋初期化」に落とす想定。
          // Lowering段階では “初期化があれば代入式” として流すと扱いやすい。
          if (let.Initializer != null)
          {
            // 変数への代入は BoundAssignmentExpression にしておくと後が楽。
            var assign = new BoundAssignmentExpression(let.Variable, let.Initializer);
            outStmts.Add(new IrExpressionStatement(assign));
          }
          else
          {
            // 初期化なしは何もしない（Udon側でゼロ初期化にするかはCodeGen側の責務）
          }
          break;

        case BoundExpressionStatement es:
          outStmts.Add(new IrExpressionStatement(es.Expression));
          break;

        case BoundReturnStatement ret:
          outStmts.Add(new IrReturn(ret.Expression));
          break;

        case BoundIfStatement ifs:
          LowerIf(ifs, outStmts);
          break;

        case BoundWhileStatement ws:
          LowerWhile(ws, outStmts);
          break;

        default:
          // 未対応ステートメントが増えたらここで診断する（DiagnosticBagを渡す設計でもOK）
          outStmts.Add(new IrExpressionStatement(new BoundErrorExpression()));
          break;
      }
    }

    private void LowerIf(BoundIfStatement ifs, List<IrStatement> outStmts)
    {
      // if (cond) then else
      // =>
      //   gotoIfFalse cond elseLabel
      //   then
      //   goto endLabel
      // elseLabel:
      //   else
      // endLabel:

      var elseLabel = NewLabel("if_else");
      var endLabel = NewLabel("if_end");

      // ★短絡評価をジャンプに落としたいので、Conditionは専用loweringを通す
      LowerConditionGotoFalse(ifs.Condition, elseLabel, outStmts);

      LowerStatement(ifs.Then, outStmts);
      outStmts.Add(new IrGoto(endLabel));

      outStmts.Add(new IrLabel(elseLabel));
      if (ifs.Else != null)
        LowerStatement(ifs.Else, outStmts);

      outStmts.Add(new IrLabel(endLabel));
    }

    private void LowerWhile(BoundWhileStatement ws, List<IrStatement> outStmts)
    {
      // while (cond) body
      // =>
      // start:
      //   gotoIfFalse cond end
      //   body
      //   goto start
      // end:

      var start = NewLabel("while_start");
      var end = NewLabel("while_end");

      outStmts.Add(new IrLabel(start));

      LowerConditionGotoFalse(ws.Condition, end, outStmts);
      LowerStatement(ws.Body, outStmts);

      outStmts.Add(new IrGoto(start));
      outStmts.Add(new IrLabel(end));
    }

    // ============================================================
    // Short-circuit lowering for conditions
    // ============================================================

    // 「cond が false なら target へ飛ぶ」を、&& / || まで含めて展開する
    private void LowerConditionGotoFalse(BoundExpression cond, LabelSymbol targetFalse, List<IrStatement> outStmts)
    {
      // cond: (a && b) が false なら targetFalse
      // =>
      //   ifFalse a goto targetFalse
      //   ifFalse b goto targetFalse
      // (a || b) が false なら targetFalse
      // =>
      //   ifTrue a goto ok
      //   ifFalse b goto targetFalse
      // ok:

      if (cond is BoundBinaryExpression bin &&
          (bin.Op.Kind == BoundBinaryOperatorKind.LogicalAnd || bin.Op.Kind == BoundBinaryOperatorKind.LogicalOr))
      {
        if (bin.Op.Kind == BoundBinaryOperatorKind.LogicalAnd)
        {
          LowerConditionGotoFalse(bin.Left, targetFalse, outStmts);
          LowerConditionGotoFalse(bin.Right, targetFalse, outStmts);
          return;
        }
        else // LogicalOr
        {
          var ok = NewLabel("or_ok");
          LowerConditionGotoTrue(bin.Left, ok, outStmts);
          LowerConditionGotoFalse(bin.Right, targetFalse, outStmts);
          outStmts.Add(new IrLabel(ok));
          return;
        }
      }

      outStmts.Add(new IrGotoIfFalse(cond, targetFalse));
    }

    // 「cond が true なら target へ飛ぶ」を、&& / || まで含めて展開する
    private void LowerConditionGotoTrue(BoundExpression cond, LabelSymbol targetTrue, List<IrStatement> outStmts)
    {
      if (cond is BoundBinaryExpression bin &&
          (bin.Op.Kind == BoundBinaryOperatorKind.LogicalAnd || bin.Op.Kind == BoundBinaryOperatorKind.LogicalOr))
      {
        if (bin.Op.Kind == BoundBinaryOperatorKind.LogicalOr)
        {
          LowerConditionGotoTrue(bin.Left, targetTrue, outStmts);
          LowerConditionGotoTrue(bin.Right, targetTrue, outStmts);
          return;
        }
        else // LogicalAnd
        {
          var cont = NewLabel("and_cont");
          LowerConditionGotoFalse(bin.Left, cont, outStmts); // left false => skip to cont (do not jump true)
          LowerConditionGotoTrue(bin.Right, targetTrue, outStmts);
          outStmts.Add(new IrLabel(cont));
          return;
        }
      }

      outStmts.Add(new IrGotoIfTrue(cond, targetTrue));
    }

    private LabelSymbol NewLabel(string prefix)
    {
      var id = ++_labelId;
      return new LabelSymbol($"{prefix}_{id}");
    }
  }
}
