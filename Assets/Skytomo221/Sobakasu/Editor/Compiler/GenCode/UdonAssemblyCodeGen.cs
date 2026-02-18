using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Skytomo221.Sobakasu.Compiler
{
  /// <summary>
  /// IR( Lowerer の出力 ) から Udon Assembly テキストを生成する。
  /// - .data_start / .data_end
  /// - .code_start / .code_end
  /// - PUSH / COPY / JUMP / JUMP_IF_FALSE / EXTERN / JUMP_INDIRECT を使用
  /// </summary>
  public sealed class UdonAssemblyCodeGen
  {
    // -----------------------------
    // Public API
    // -----------------------------

    public string Generate(IrProgram program)
    {
      _sb.Clear();
      _dataVars.Clear();
      _varNameMap.Clear();
      _constPool.Clear();
      _tempId = 0;

      // 1) 変数宣言（data部を先に集める）
      Predeclare(program);

      // 2) data
      EmitDataSection();

      // 3) code
      EmitCodeSection(program);

      return _sb.ToString();
    }

    // -----------------------------
    // Internals: state
    // -----------------------------

    private readonly StringBuilder _sb = new(16 * 1024);

    // data var registry: name -> (udonType, initLiteral)
    private readonly Dictionary<string, (string udonType, string init)> _dataVars
        = new(StringComparer.Ordinal);

    // map symbols to udon var names
    private readonly Dictionary<VariableSymbol, string> _varNameMap = new();

    // const pool: key -> varName
    private readonly Dictionary<(TypeSymbol type, object? value), string> _constPool = new();

    private int _tempId;

    // Common reserved vars used for ending execution / indirect jump
    private const string JumpAddrVar = "__jump_addr"; // %SystemUInt32
    private const string ExitAddrVar = "__exit_addr"; // %SystemUInt32 = 0xFFFFFFFC

    // -----------------------------
    // Phase 1: predeclare vars
    // -----------------------------

    private void Predeclare(IrProgram program)
    {
      // required control vars
      DeclareDataVar(ExitAddrVar, "%SystemUInt32", "0xFFFFFFFC"); // 0xFFFFFFFC へ遷移すると終了 :contentReference[oaicite:2]{index=2}
      DeclareDataVar(JumpAddrVar, "%SystemUInt32", "0xFFFFFFFC");

      foreach (var fn in program.Functions)
      {
        CollectVarsFromBlock(fn.Body);
      }
    }

    private void CollectVarsFromBlock(IrBlock block)
    {
      foreach (var st in block.Statements)
      {
        switch (st)
        {
          case IrGotoIfFalse gif:
            CollectVarsFromExpr(gif.Condition);
            break;
          case IrGotoIfTrue git:
            CollectVarsFromExpr(git.Condition);
            break;
          case IrExpressionStatement es:
            CollectVarsFromExpr(es.Expression);
            break;
          case IrReturn ret:
            if (ret.Expression != null)
              CollectVarsFromExpr(ret.Expression);
            break;
          default:
            break;
        }
      }
    }

    private void CollectVarsFromExpr(BoundExpression expr)
    {
      switch (expr)
      {
        case BoundVariableExpression v:
          EnsureVariable(v.Variable);
          break;

        case BoundAssignmentExpression a:
          EnsureVariable(a.Variable);
          CollectVarsFromExpr(a.Expression);
          break;

        case BoundUnaryExpression u:
          CollectVarsFromExpr(u.Operand);
          break;

        case BoundBinaryExpression b:
          CollectVarsFromExpr(b.Left);
          CollectVarsFromExpr(b.Right);
          break;

        case BoundCallExpression c:
          // 引数は必要
          foreach (var arg in c.Arguments)
            CollectVarsFromExpr(arg);

          // NOTE: ここで戻り値を持つ関数なら戻り値用tempが要るが、
          // 現状 Binder は void 関数のみ想定だったはずなので省略。
          break;

        case BoundLiteralExpression lit:
          EnsureConst(lit.Type, lit.Value);
          break;

        default:
          break;
      }
    }

    private string EnsureVariable(VariableSymbol v)
    {
      if (_varNameMap.TryGetValue(v, out var name))
        return name;

      // Udonでは予約名回避に _ を推奨、この記事でも言及 :contentReference[oaicite:3]{index=3}
      name = "_" + Sanitize(v.Name);

      _varNameMap.Add(v, name);

      DeclareDataVar(name, ToUdonTypeName(v.Type), DefaultInitLiteral(v.Type));
      return name;
    }

    private string EnsureTemp(TypeSymbol type)
    {
      var name = $"__tmp_{++_tempId}";
      DeclareDataVar(name, ToUdonTypeName(type), DefaultInitLiteral(type));
      return name;
    }

    private string EnsureConst(TypeSymbol type, object? value)
    {
      var key = (type, value);
      if (_constPool.TryGetValue(key, out var name))
        return name;

      name = $"__k_{_constPool.Count}";
      _constPool.Add(key, name);

      DeclareDataVar(name, ToUdonTypeName(type), FormatLiteral(type, value));
      return name;
    }

    private void DeclareDataVar(string name, string udonType, string initLiteral)
    {
      if (_dataVars.ContainsKey(name))
        return;

      _dataVars.Add(name, (udonType, initLiteral));
    }

    // -----------------------------
    // Emit: data section
    // -----------------------------

    private void EmitDataSection()
    {
      _sb.AppendLine(".data_start");
      _sb.AppendLine();

      foreach (var kv in _dataVars)
      {
        var name = kv.Key;
        var (udonType, init) = kv.Value;
        _sb.Append("    ").Append(name).Append(": ").Append(udonType).Append(", ").Append(init).AppendLine();
      }

      _sb.AppendLine();
      _sb.AppendLine(".data_end");
      _sb.AppendLine();
    }

    // -----------------------------
    // Emit: code section
    // -----------------------------

    private void EmitCodeSection(IrProgram program)
    {
      _sb.AppendLine(".code_start");

      foreach (var fn in program.Functions)
      {
        var label = FunctionLabel(fn.Symbol);

        // event entrypoints: .export label でイベントとして認識 :contentReference[oaicite:4]{index=4}
        if (fn.Symbol.IsEventEntry)
        {
          _sb.Append("    .export ").Append(label).AppendLine();
        }

        _sb.Append("    ").Append(label).Append(":").AppendLine();

        // “エントリポイントから return したら終了” を保証したいなら、
        // 最後に JUMP_INDIRECT で ExitAddrVar へ飛ばすのが安全。
        //（0xFFFFFFFC へ遷移で終了、という慣習） :contentReference[oaicite:5]{index=5}

        foreach (var st in fn.Body.Statements)
          EmitStatement(st);

        // 関数末尾は終了
        EmitPush(ExitAddrVar);
        EmitPush(JumpAddrVar);
        EmitCopy();              // __jump_addr <- __exit_addr
        EmitJumpIndirect(JumpAddrVar);

        _sb.AppendLine();
      }

      _sb.AppendLine(".code_end");
    }

    private void EmitStatement(IrStatement st)
    {
      switch (st)
      {
        case IrLabel lab:
          _sb.Append("    ").Append(lab.Label.Name).Append(":").AppendLine();
          break;

        case IrGoto g:
          EmitJump(g.Target.Name);
          break;

        case IrGotoIfFalse gf:
          {
            var condVar = EmitEvalToVar(gf.Condition);
            EmitPush(condVar);
            EmitJumpIfFalse(gf.Target.Name); // JUMP_IF_FALSE consumes 1 boolean :contentReference[oaicite:6]{index=6}
            break;
          }

        case IrGotoIfTrue gt:
          {
            // Udonに JUMP_IF_TRUE はない（少なくとも公式/解説では列挙されていない） :contentReference[oaicite:7]{index=7}
            // なので:
            //   if (cond) goto T
            // を
            //   PUSH cond; JUMP_IF_FALSE skip; JUMP T; skip:
            // に展開する
            var skip = $"__iftrue_skip_{++_tempId}";
            var condVar = EmitEvalToVar(gt.Condition);
            EmitPush(condVar);
            EmitJumpIfFalse(skip);
            EmitJump(gt.Target.Name);
            _sb.Append("    ").Append(skip).Append(":").AppendLine();
            break;
          }

        case IrExpressionStatement es:
          EmitExpressionStatement(es.Expression);
          break;

        case IrReturn ret:
          // 現状は void のみ想定。終了へ。
          EmitPush(ExitAddrVar);
          EmitPush(JumpAddrVar);
          EmitCopy();
          EmitJumpIndirect(JumpAddrVar);
          break;

        default:
          // 未対応
          break;
      }
    }

    private void EmitExpressionStatement(BoundExpression expr)
    {
      // 代入は COPY に落とす :contentReference[oaicite:8]{index=8}
      if (expr is BoundAssignmentExpression a)
      {
        var rhsVar = EmitEvalToVar(a.Expression);
        var dstVar = EnsureVariable(a.Variable);

        EmitPush(rhsVar);
        EmitPush(dstVar);
        EmitCopy();
        return;
      }

      // void呼び出しなど
      if (expr is BoundCallExpression call)
      {
        EmitCall(call, discardReturnValue: true);
        return;
      }

      // それ以外は評価だけして捨てる（POP命令がある） :contentReference[oaicite:9]{index=9}
      var v = EmitEvalToVar(expr);
      EmitPush(v);
      EmitPop();
    }

    // -----------------------------
    // Expression codegen (stack + extern)
    // -----------------------------

    /// <summary>
    /// expr を評価し、その結果が入った “変数名” を返す。
    /// Udon Assembly ではリテラルも含め全て変数として扱うのが基本 :contentReference[oaicite:10]{index=10}
    /// </summary>
    private string EmitEvalToVar(BoundExpression expr)
    {
      switch (expr)
      {
        case BoundLiteralExpression lit:
          return EnsureConst(lit.Type, lit.Value);

        case BoundVariableExpression v:
          return EnsureVariable(v.Variable);

        case BoundAssignmentExpression a:
          {
            var rhs = EmitEvalToVar(a.Expression);
            var dst = EnsureVariable(a.Variable);
            EmitPush(rhs);
            EmitPush(dst);
            EmitCopy();
            return dst;
          }

        case BoundUnaryExpression u:
          return EmitUnary(u);

        case BoundBinaryExpression b:
          return EmitBinary(b);

        case BoundCallExpression c:
          // 現状は戻り値なし想定（void）。戻り値が要るなら temp を用意して PUSH, retVar して EXTERN。
          EmitCall(c, discardReturnValue: false);
          // void として扱い、dummy bool/int を返すのは危険なので error temp を返す
          return EnsureTemp(TypeSymbol.Error);

        case BoundErrorExpression:
        default:
          return EnsureTemp(TypeSymbol.Error);
      }
    }

    private string EmitUnary(BoundUnaryExpression u)
    {
      var operandVar = EmitEvalToVar(u.Operand);
      var result = EnsureTemp(u.Type);

      // EXTERN の命名規則（型名 + __メソッド名__引数__戻り値） :contentReference[oaicite:11]{index=11}
      // 例: SystemInt32.__op_UnaryNegation__SystemInt32__SystemInt32 など（推定）
      var externName = u.Op.Kind switch
      {
        BoundUnaryOperatorKind.Identity => null, // +x はそのまま
        BoundUnaryOperatorKind.Negation => $"{UdonTypeOwner(u.Operand.Type)}.__op_UnaryNegation__{UdonTypeSig(u.Operand.Type)}__{UdonTypeSig(u.Type)}",
        BoundUnaryOperatorKind.LogicalNegation => $"{UdonTypeOwner(TypeSymbol.Bool)}.__op_LogicalNot__{UdonTypeSig(TypeSymbol.Bool)}__{UdonTypeSig(TypeSymbol.Bool)}",
        _ => null
      };

      if (externName is null)
        return operandVar;

      EmitPush(operandVar);
      EmitPush(result);
      EmitExtern(externName);
      return result;
    }

    private string EmitBinary(BoundBinaryExpression b)
    {
      var leftVar = EmitEvalToVar(b.Left);
      var rightVar = EmitEvalToVar(b.Right);
      var result = EnsureTemp(b.Type);

      var externName = ResolveBinaryExtern(b.Op.Kind, b.Left.Type, b.Right.Type, b.Type);

      // UdonAssemblyに四則演算命令はなく、EXTERNで op_Addition 等を呼ぶ :contentReference[oaicite:12]{index=12}
      EmitPush(leftVar);
      EmitPush(rightVar);
      EmitPush(result);
      EmitExtern(externName);

      return result;
    }

    private void EmitCall(BoundCallExpression call, bool discardReturnValue)
    {
      // ここは “自作言語の関数呼び出し” をやるなら
      // JUMP/JUMP_INDIRECT で疑似コールを組む（Qiitaの例） :contentReference[oaicite:13]{index=13}
      // ただし、あなたの現状の IR/Lowering だと call は式の中にも出る可能性があり、
      // 返り値 / 引数領域 / 戻り先管理をしっかり設計する必要がある。
      //
      // まずは「ビルトインEXTERN呼び出し」だけ対応しておくのが安全。

      var fn = call.Function;

      // ✅ 例: Debug.Log 相当（必要ならあなた側でビルトイン関数として用意）
      // "UnityEngineDebug.__Log__SystemObject__SystemVoid" は記事の例に存在 :contentReference[oaicite:14]{index=14}
      if (string.Equals(fn.Name, "Log", StringComparison.Ordinal) ||
          string.Equals(fn.Name, "DebugLog", StringComparison.Ordinal))
      {
        if (call.Arguments.Count != 1)
        {
          // ここは Diagnostics でエラーにするのが本当は良い
          return;
        }

        var arg0 = EmitEvalToVar(call.Arguments[0]);
        EmitPush(arg0);
        EmitExtern("UnityEngineDebug.__Log__SystemObject__SystemVoid");
        return;
      }

      // ❌ 未実装（ユーザー定義関数コール）
      // ここを今すぐ実装するなら、下記の追加設計が必要:
      // - すべての関数に「引数領域（data var）」を割り当てる
      // - 返り値領域（data var）と return address の受け渡し
      // - JUMP/JUMP_INDIRECT による復帰（Qiitaの疑似コール） :contentReference[oaicite:15]{index=15}
    }

    // -----------------------------
    // Udon opcode emit helpers
    // -----------------------------

    private void EmitPush(string varOrLiteral)
        => _sb.Append("        PUSH, ").Append(varOrLiteral).AppendLine();

    private void EmitPop()
        => _sb.AppendLine("        POP");

    private void EmitCopy()
        => _sb.AppendLine("        COPY");

    private void EmitJump(string label)
        => _sb.Append("        JUMP, ").Append(label).AppendLine();

    private void EmitJumpIfFalse(string label)
        => _sb.Append("        JUMP_IF_FALSE, ").Append(label).AppendLine();

    private void EmitJumpIndirect(string varName)
        => _sb.Append("        JUMP_INDIRECT, ").Append(varName).AppendLine();

    private void EmitExtern(string externName)
        => _sb.Append("        EXTERN, \"").Append(externName).Append('"').AppendLine();

    // -----------------------------
    // Naming
    // -----------------------------

    private static string FunctionLabel(FunctionSymbol fn)
    {
      if (fn.IsEventEntry)
      {
        // イベントは _start / _update / _onPlayerJoined のようにする :contentReference[oaicite:16]{index=16}
        return EventToLabel(fn.UdonEventName ?? fn.Name);
      }

      // non-event functions: you can pick your own convention
      return "fn_" + Sanitize(fn.Name);
    }

    private static string EventToLabel(string udonEventName)
    {
      // Start -> _start, OnPlayerJoined -> _onPlayerJoined
      if (string.IsNullOrEmpty(udonEventName))
        return "_event";

      // lower first char only (PascalCase -> camelCase)
      var first = char.ToLowerInvariant(udonEventName[0]);
      var rest = udonEventName.Length > 1 ? udonEventName.Substring(1) : "";
      return "_" + first + rest;
    }

    private static string Sanitize(string s)
    {
      if (string.IsNullOrEmpty(s))
        return "x";

      var sb = new StringBuilder(s.Length);
      foreach (var ch in s)
      {
        if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '<' || ch == '>' || ch == '[' || ch == ']')
          sb.Append(ch);
        else
          sb.Append('_');
      }
      return sb.ToString();
    }

    // -----------------------------
    // Type mapping
    // -----------------------------

    private static string ToUdonTypeName(TypeSymbol t)
    {
      if (ReferenceEquals(t, TypeSymbol.Int)) return "%SystemInt32";
      if (ReferenceEquals(t, TypeSymbol.Float)) return "%SystemSingle";
      if (ReferenceEquals(t, TypeSymbol.Bool)) return "%SystemBoolean";
      if (ReferenceEquals(t, TypeSymbol.String)) return "%SystemString";
      if (ReferenceEquals(t, TypeSymbol.Void)) return "%SystemVoid";
      if (ReferenceEquals(t, TypeSymbol.Error)) return "%SystemObject"; // fallback
      return "%SystemObject";
    }

    private static string UdonTypeOwner(TypeSymbol t)
    {
      // EXTERN 名で使う “型名” 側（%なし）
      if (ReferenceEquals(t, TypeSymbol.Int)) return "SystemInt32";
      if (ReferenceEquals(t, TypeSymbol.Float)) return "SystemSingle";
      if (ReferenceEquals(t, TypeSymbol.Bool)) return "SystemBoolean";
      if (ReferenceEquals(t, TypeSymbol.String)) return "SystemString";
      if (ReferenceEquals(t, TypeSymbol.Void)) return "SystemVoid";
      return "SystemObject";
    }

    private static string UdonTypeSig(TypeSymbol t) => UdonTypeOwner(t);

    private static string DefaultInitLiteral(TypeSymbol t)
    {
      if (ReferenceEquals(t, TypeSymbol.Int)) return "0";
      if (ReferenceEquals(t, TypeSymbol.Float)) return "0.0";
      if (ReferenceEquals(t, TypeSymbol.Bool)) return "False";
      if (ReferenceEquals(t, TypeSymbol.String)) return "null";
      if (ReferenceEquals(t, TypeSymbol.Void)) return "null";
      return "null";
    }

    private static string FormatLiteral(TypeSymbol t, object? value)
    {
      if (ReferenceEquals(t, TypeSymbol.Int))
        return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

      if (ReferenceEquals(t, TypeSymbol.Float))
        return Convert.ToSingle(value ?? 0f, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

      if (ReferenceEquals(t, TypeSymbol.Bool))
        return (value is bool b && b) ? "True" : "False";

      if (ReferenceEquals(t, TypeSymbol.String))
      {
        if (value is null) return "null";
        var s = value.ToString() ?? "";
        s = s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{s}\"";
      }

      // default
      return "null";
    }

    // -----------------------------
    // Binary extern mapping
    // -----------------------------

    private static string ResolveBinaryExtern(
        BoundBinaryOperatorKind kind,
        TypeSymbol left,
        TypeSymbol right,
        TypeSymbol result)
    {
      // 命名規則自体は公式/解説に記載 :contentReference[oaicite:17]{index=17}
      // ここでは典型的な operator 名で組み立てる（必要に応じて調整）
      var owner = UdonTypeOwner(left); // left==right 前提（Binder側で整合している想定）
      var a = UdonTypeSig(left);
      var b = UdonTypeSig(right);
      var r = UdonTypeSig(result);

      var opName = kind switch
      {
        BoundBinaryOperatorKind.Add => "op_Addition",
        BoundBinaryOperatorKind.Subtract => "op_Subtraction",
        BoundBinaryOperatorKind.Multiply => "op_Multiply",
        BoundBinaryOperatorKind.Divide => "op_Division",

        BoundBinaryOperatorKind.Equals => "op_Equality",
        BoundBinaryOperatorKind.NotEquals => "op_Inequality",

        BoundBinaryOperatorKind.Less => "op_LessThan",
        BoundBinaryOperatorKind.LessOrEqual => "op_LessThanOrEqual",
        BoundBinaryOperatorKind.Greater => "op_GreaterThan",
        BoundBinaryOperatorKind.GreaterOrEqual => "op_GreaterThanOrEqual",

        // 論理and/orは短絡を Lowering でジャンプ化している想定。
        // それでも式として残る場合は & / | 的な op を呼ぶ方が安全だが、
        // ここでは一旦 bool の op_BitwiseAnd / op_BitwiseOr に寄せる。
        BoundBinaryOperatorKind.LogicalAnd => "op_BitwiseAnd",
        BoundBinaryOperatorKind.LogicalOr => "op_BitwiseOr",

        _ => throw new InvalidOperationException($"Unsupported binary operator: {kind}")
      };

      return $"{owner}.__{opName}__{a}_{b}__{r}";
    }
  }
}
