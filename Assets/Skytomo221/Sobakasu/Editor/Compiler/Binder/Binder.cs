using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{
public sealed class Binder
  {
    public DiagnosticBag Diagnostics { get; } = new();

    private readonly Dictionary<string, FunctionSymbol> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<FunctionSymbol, BoundBlockStatement> _bodies = new();

    // current function context
    private FunctionSymbol? _currentFunction;
    private BoundScope? _scope;

    // event name mapping (DSL name -> Udon event)
    private static readonly Dictionary<string, string> _eventMap = new(StringComparer.Ordinal)
    {
      // minimal examples; add as you need
      ["PlayerJoined"] = "OnPlayerJoined",
      ["PlayerLeft"] = "OnPlayerLeft",
      ["Interact"] = "Interact",
      ["Start"] = "Start",
      ["Update"] = "Update",
    };

    public BoundProgram Bind(CompilationUnitSyntax root)
    {
      // 1) Declare all top-level callables first (for forward calls)
      DeclareTopLevel(root);

      // 2) Bind bodies
      foreach (var decl in root.Declarations)
      {
        if (decl is OnDeclSyntax onDecl)
          BindOnDeclBody(onDecl);
        else if (decl is FuncDeclSyntax fnDecl)
          BindFuncDeclBody(fnDecl);
      }

      // 3) Udon-ish constraint: recursion not allowed (direct/indirect)
      CheckRecursion();

      var funcs = new List<FunctionSymbol>(_functions.Values);
      return new BoundProgram(funcs, _bodies);
    }

    private void DeclareTopLevel(CompilationUnitSyntax root)
    {
      foreach (var decl in root.Declarations)
      {
        if (decl is OnDeclSyntax onDecl)
          DeclareOn(onDecl);
        else if (decl is FuncDeclSyntax fnDecl)
          DeclareFunc(fnDecl);
      }
    }

    private void DeclareOn(OnDeclSyntax onDecl)
    {
      var dslName = onDecl.EventName.Text;
      if (!_eventMap.TryGetValue(dslName, out var udonName))
      {
        Diagnostics.ReportError(onDecl.EventName.Span, $"未対応のイベントです: '{dslName}'");
        udonName = dslName; // fallback: keep as-is
      }

      // event entries are procedures: return void, no params (for now)
      var sym = new FunctionSymbol(
          name: $"on::{dslName}",
          parameters: Array.Empty<ParameterSymbol>(),
          returnType: TypeSymbol.Void,
          isEventEntry: true,
          udonEventName: udonName);

      if (!_functions.TryAdd(sym.Name, sym))
        Diagnostics.ReportError(onDecl.EventName.Span, $"同名の on ハンドラが重複しています: '{dslName}'");
    }

    private void DeclareFunc(FuncDeclSyntax fnDecl)
    {
      var name = fnDecl.Name.Text;

      if (_functions.ContainsKey(name))
      {
        Diagnostics.ReportError(fnDecl.Name.Span, $"関数名が重複しています: '{name}'（オーバーロード非対応）");
        return;
      }

      var parameters = new List<ParameterSymbol>(fnDecl.Parameters.Count);
      var seen = new HashSet<string>(StringComparer.Ordinal);

      foreach (var p in fnDecl.Parameters)
      {
        var pName = p.Name.Text;
        if (!seen.Add(pName))
          Diagnostics.ReportError(p.Name.Span, $"引数名が重複しています: '{pName}'");

        var type = ResolveType(p.TypeName);
        parameters.Add(new ParameterSymbol(pName, type));
      }

      // for now: procedures only (void). If you add returns later, change here + Return binding.
      var sym = new FunctionSymbol(
          name: name,
          parameters: parameters,
          returnType: TypeSymbol.Void,
          isEventEntry: false,
          udonEventName: null);

      _functions.Add(name, sym);
    }

    private void BindOnDeclBody(OnDeclSyntax onDecl)
    {
      var symName = $"on::{onDecl.EventName.Text}";
      if (!_functions.TryGetValue(symName, out var fn))
        return;

      BindFunctionBody(fn, onDecl.Body);
    }

    private void BindFuncDeclBody(FuncDeclSyntax fnDecl)
    {
      var name = fnDecl.Name.Text;
      if (!_functions.TryGetValue(name, out var fn))
        return;

      BindFunctionBody(fn, fnDecl.Body);
    }

    private void BindFunctionBody(FunctionSymbol function, BlockSyntax body)
    {
      _currentFunction = function;
      _scope = new BoundScope(parent: null);

      // declare parameters in scope
      foreach (var p in function.Parameters)
      {
        var v = new VariableSymbol(p.Name, p.Type, isReadOnly: true);
        _scope.TryDeclare(v); // duplicates already diagnosed
      }

      var boundBody = BindBlock(body);

      _bodies[function] = boundBody;

      _currentFunction = null;
      _scope = null;
    }

    private BoundBlockStatement BindBlock(BlockSyntax block)
    {
      // new nested scope per block
      _scope = new BoundScope(_scope);

      var list = new List<BoundStatement>(block.Statements.Count);
      foreach (var s in block.Statements)
        list.Add(BindStatement(s));

      _scope = _scope.Parent;
      return new BoundBlockStatement(list);
    }

    private BoundStatement BindStatement(StatementSyntax stmt)
    {
      return stmt switch
      {
        BlockSyntax b => BindBlock(b),
        LetStatementSyntax let => BindLet(let),
        IfStatementSyntax ifs => BindIf(ifs),
        WhileStatementSyntax ws => BindWhile(ws),
        ReturnStatementSyntax rs => BindReturn(rs),
        ExpressionStatementSyntax es => new BoundExpressionStatement(BindExpression(es.Expression)),
        _ => new BoundExpressionStatement(new BoundErrorExpression()),
      };
    }

    private BoundStatement BindLet(LetStatementSyntax let)
    {
      var name = let.Name.Text;

      var declaredType = let.TypeName is null ? null : ResolveType(let.TypeName);
      BoundExpression? init = null;

      if (let.Initializer is not null)
        init = BindExpression(let.Initializer);

      var finalType = declaredType ?? init?.Type ?? TypeSymbol.Error;

      // type check initializer if explicit type exists
      if (declaredType is not null && init is not null && !CanAssign(declaredType, init.Type))
      {
        Diagnostics.ReportError(let.Initializer.Span,
            $"型が一致しません: '{declaredType}' に '{init.Type}' は代入できません");
        init = new BoundErrorExpression();
      }

      var variable = new VariableSymbol(name, finalType, isReadOnly: false);

      if (_scope is null || !_scope.TryDeclare(variable))
        Diagnostics.ReportError(let.Name.Span, $"変数名が重複しています: '{name}'");

      return new BoundLetStatement(variable, init);
    }

    private BoundStatement BindIf(IfStatementSyntax ifs)
    {
      var cond = BindExpression(ifs.Condition);
      cond = RequireType(cond, TypeSymbol.Bool, ifs.Condition.Span);

      var thenStmt = BindStatement(ifs.Then);
      var elseStmt = ifs.Else is null ? null : BindStatement(ifs.Else);

      return new BoundIfStatement(cond, thenStmt, elseStmt);
    }

    private BoundStatement BindWhile(WhileStatementSyntax ws)
    {
      var cond = BindExpression(ws.Condition);
      cond = RequireType(cond, TypeSymbol.Bool, ws.Condition.Span);

      var body = BindStatement(ws.Body);
      return new BoundWhileStatement(cond, body);
    }

    private BoundStatement BindReturn(ReturnStatementSyntax rs)
    {
      // current design: procedures only (void)
      if (rs.Expression is not null)
      {
        Diagnostics.ReportError(rs.Expression.Span, "この言語バージョンでは return に値を返せません（void のみ）");
        _ = BindExpression(rs.Expression); // still bind to collect errors inside
      }

      return new BoundReturnStatement(null);
    }

    private BoundExpression BindExpression(ExpressionSyntax expr)
    {
      return expr switch
      {
        ErrorExpressionSyntax => new BoundErrorExpression(),
        LiteralExpressionSyntax lit => BindLiteral(lit),
        NameExpressionSyntax name => BindName(name),
        UnaryExpressionSyntax un => BindUnary(un),
        BinaryExpressionSyntax bin => BindBinary(bin),
        AssignmentExpressionSyntax assign => BindAssignment(assign),
        CallExpressionSyntax call => BindCall(call),
        ParenthesizedExpressionSyntax paren => BindExpression(paren.Expression),
        _ => new BoundErrorExpression(),
      };
    }

    private BoundExpression BindLiteral(LiteralExpressionSyntax lit)
    {
      // map literal token kinds to types
      return lit.LiteralToken.Kind switch
      {
        TokenKind.Number => new BoundLiteralExpression(lit.Value ?? 0, TypeSymbol.Int),
        TokenKind.String => new BoundLiteralExpression(lit.Value ?? "", TypeSymbol.String),
        TokenKind.True => new BoundLiteralExpression(true, TypeSymbol.Bool),
        TokenKind.False => new BoundLiteralExpression(false, TypeSymbol.Bool),
        _ => new BoundErrorExpression()
      };
    }

    private BoundExpression BindName(NameExpressionSyntax name)
    {
      var id = name.Identifier.Text;
      var v = _scope?.TryLookup(id);

      if (v is null)
      {
        Diagnostics.ReportError(name.Identifier.Span, $"未定義の変数です: '{id}'");
        return new BoundErrorExpression();
      }

      return new BoundVariableExpression(v);
    }

    private BoundExpression BindUnary(UnaryExpressionSyntax un)
    {
      var operand = BindExpression(un.Operand);
      var op = BoundUnaryOperator.Bind(un.OperatorToken.Kind, operand.Type);

      if (op is null)
      {
        Diagnostics.ReportError(un.OperatorToken.Span,
            $"単項演算子 '{un.OperatorToken.Text}' は型 '{operand.Type}' に適用できません");
        return new BoundErrorExpression();
      }

      return new BoundUnaryExpression(op, operand);
    }

    private BoundExpression BindBinary(BinaryExpressionSyntax bin)
    {
      var left = BindExpression(bin.Left);
      var right = BindExpression(bin.Right);

      var op = BoundBinaryOperator.Bind(bin.OperatorToken.Kind, left.Type, right.Type);

      // small convenience: int <-> float numeric promotion (optional)
      // If you want strict typing, remove this whole block.
      if (op is null && TypeSymbol.IsNumeric(left.Type) && TypeSymbol.IsNumeric(right.Type))
      {
        // promote int->float if mixed
        if (ReferenceEquals(left.Type, TypeSymbol.Int) && ReferenceEquals(right.Type, TypeSymbol.Float))
          left = RequireType(left, TypeSymbol.Float, bin.Left.Span);
        else if (ReferenceEquals(left.Type, TypeSymbol.Float) && ReferenceEquals(right.Type, TypeSymbol.Int))
          right = RequireType(right, TypeSymbol.Float, bin.Right.Span);

        op = BoundBinaryOperator.Bind(bin.OperatorToken.Kind, left.Type, right.Type);
      }

      if (op is null)
      {
        Diagnostics.ReportError(bin.OperatorToken.Span,
            $"二項演算子 '{bin.OperatorToken.Text}' は '{left.Type}' と '{right.Type}' の組み合わせに適用できません");
        return new BoundErrorExpression();
      }

      return new BoundBinaryExpression(left, op, right);
    }

    private BoundExpression BindAssignment(AssignmentExpressionSyntax assign)
    {
      var name = assign.Identifier.Text;
      var v = _scope?.TryLookup(name);

      if (v is null)
      {
        Diagnostics.ReportError(assign.Identifier.Span, $"未定義の変数に代入しています: '{name}'");
        // still bind RHS for extra errors
        _ = BindExpression(assign.Expression);
        return new BoundErrorExpression();
      }

      if (v.IsReadOnly)
        Diagnostics.ReportError(assign.Identifier.Span, $"読み取り専用変数に代入できません: '{name}'");

      var rhs = BindExpression(assign.Expression);

      if (!CanAssign(v.Type, rhs.Type))
      {
        Diagnostics.ReportError(assign.Expression.Span,
            $"型が一致しません: '{v.Type}' に '{rhs.Type}' は代入できません");
        rhs = new BoundErrorExpression();
      }

      return new BoundAssignmentExpression(v, rhs);
    }

    private BoundExpression BindCall(CallExpressionSyntax call)
    {
      // for now, only support calling a simple identifier:
      // Foo(1,2)  / not (obj.Foo()) yet
      if (call.Callee is not NameExpressionSyntax calleeName)
      {
        Diagnostics.ReportError(call.Callee.Span, "現在は識別子呼び出しのみ対応です（例: Foo(...)）");
        // bind args anyway
        var tmp = new List<BoundExpression>(call.Arguments.Count);
        foreach (var a in call.Arguments) tmp.Add(BindExpression(a));
        return new BoundErrorExpression();
      }

      var fnName = calleeName.Identifier.Text;

      if (!_functions.TryGetValue(fnName, out var fn))
      {
        Diagnostics.ReportError(calleeName.Identifier.Span, $"未定義の関数です: '{fnName}'");
        // bind args anyway
        foreach (var a in call.Arguments) _ = BindExpression(a);
        return new BoundErrorExpression();
      }

      // events cannot be called like normal functions
      if (fn.IsEventEntry)
        Diagnostics.ReportError(calleeName.Identifier.Span, $"イベントハンドラは呼び出せません: '{fnName}'");

      if (call.Arguments.Count != fn.Parameters.Count)
      {
        Diagnostics.ReportError(call.LParen.Span,
            $"引数の数が一致しません: '{fnName}' は {fn.Parameters.Count} 個ですが {call.Arguments.Count} 個渡されています");
      }

      var boundArgs = new List<BoundExpression>(call.Arguments.Count);
      int n = Math.Min(call.Arguments.Count, fn.Parameters.Count);

      for (int i = 0; i < call.Arguments.Count; i++)
      {
        var argExpr = BindExpression(call.Arguments[i]);

        if (i < n)
        {
          var paramType = fn.Parameters[i].Type;
          if (!CanAssign(paramType, argExpr.Type))
          {
            Diagnostics.ReportError(call.Arguments[i].Span,
                $"引数{i + 1}の型が一致しません: '{paramType}' が必要ですが '{argExpr.Type}' です");
            argExpr = new BoundErrorExpression();
          }
          else
          {
            // optional: insert implicit conversion nodes later
          }
        }

        boundArgs.Add(argExpr);
      }

      // recursion graph build later in CheckRecursion(); but we also keep calls for that pass
      RecordCallEdge(_currentFunction, fn);

      return new BoundCallExpression(fn, boundArgs);
    }

    // ============================================================
    // Helpers: types, conversions, recursion checks
    // ============================================================

    private TypeSymbol ResolveType(Token typeNameToken)
    {
      var text = typeNameToken.Text;

      // builtin types
      if (string.Equals(text, "void", StringComparison.Ordinal)) return TypeSymbol.Void;
      if (string.Equals(text, "bool", StringComparison.Ordinal)) return TypeSymbol.Bool;
      if (string.Equals(text, "int", StringComparison.Ordinal)) return TypeSymbol.Int;
      if (string.Equals(text, "float", StringComparison.Ordinal)) return TypeSymbol.Float;
      if (string.Equals(text, "string", StringComparison.Ordinal)) return TypeSymbol.String;

      Diagnostics.ReportError(typeNameToken.Span, $"未対応の型です: '{text}'");
      return TypeSymbol.Error;
    }

    private static bool CanAssign(TypeSymbol target, TypeSymbol source)
    {
      if (ReferenceEquals(target, TypeSymbol.Error) || ReferenceEquals(source, TypeSymbol.Error))
        return true;

      if (ReferenceEquals(target, source))
        return true;

      // optional numeric widening: int -> float
      if (ReferenceEquals(target, TypeSymbol.Float) && ReferenceEquals(source, TypeSymbol.Int))
        return true;

      return false;
    }

    private BoundExpression RequireType(BoundExpression expr, TypeSymbol required, TextSpan spanForError)
    {
      if (ReferenceEquals(expr.Type, TypeSymbol.Error))
        return expr;

      if (ReferenceEquals(expr.Type, required))
        return expr;

      // implicit int->float conversion point (if you later add conversion nodes, insert here)
      if (ReferenceEquals(required, TypeSymbol.Float) && ReferenceEquals(expr.Type, TypeSymbol.Int))
        return expr; // treat as OK for now

      Diagnostics.ReportError(spanForError, $"型が必要です: '{required}'（実際: '{expr.Type}'）");
      return new BoundErrorExpression();
    }

    // ---- recursion detection (no recursion for Udon) ----
    private readonly Dictionary<FunctionSymbol, HashSet<FunctionSymbol>> _callGraph = new();

    private void RecordCallEdge(FunctionSymbol? caller, FunctionSymbol callee)
    {
      if (caller is null)
        return; // top-level should not call, but safe

      if (!_callGraph.TryGetValue(caller, out var set))
      {
        set = new HashSet<FunctionSymbol>();
        _callGraph[caller] = set;
      }
      set.Add(callee);
    }

    private void CheckRecursion()
    {
      // DFS cycle detection
      var temp = new HashSet<FunctionSymbol>();
      var perm = new HashSet<FunctionSymbol>();

      foreach (var fn in _functions.Values)
        Visit(fn);

      void Visit(FunctionSymbol fn)
      {
        if (perm.Contains(fn)) return;
        if (temp.Contains(fn))
        {
          // cycle found
          Diagnostics.ReportError(new TextSpan(0, 0, 1, 1),
              $"再帰が検出されました（Udon向け制約）: '{fn.Name}' を含む呼び出し循環");
          return;
        }

        temp.Add(fn);

        if (_callGraph.TryGetValue(fn, out var next))
        {
          foreach (var callee in next)
            Visit(callee);
        }

        temp.Remove(fn);
        perm.Add(fn);
      }
    }
  }
}
