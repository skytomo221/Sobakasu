using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Lexer;

namespace Skytomo221.Sobakasu.Compiler
{
public sealed class SobakasuParser
  {
    private readonly Token[] _tokens;
    private int _pos;

    public DiagnosticBag Diagnostics { get; }

    public SobakasuParser(string source)
    {
      Diagnostics = new DiagnosticBag();

      var tokens = new List<Token>(256);
      var lexer = new SobakasuLexer(source ?? "", Diagnostics);

      Token t;
      do
      {
        t = lexer.NextToken();
        tokens.Add(t);
      } while (t.Kind != TokenKind.EOF);

      _tokens = tokens.ToArray();
    }

    private Token Current => Peek(0);
    private Token Peek(int offset)
    {
      int index = _pos + offset;
      if (index < 0) index = 0;
      if (index >= _tokens.Length) index = _tokens.Length - 1;
      return _tokens[index];
    }

    private Token NextToken()
    {
      var t = Current;
      _pos = Math.Min(_pos + 1, _tokens.Length - 1);
      return t;
    }

    private bool TryConsume(TokenKind kind, out Token token)
    {
      if (Current.Kind == kind)
      {
        token = NextToken();
        return true;
      }
      token = default!;
      return false;
    }

    private Token Match(TokenKind kind)
    {
      if (Current.Kind == kind)
        return NextToken();

      Diagnostics.ReportError(Current.Span, $"'{kind}' が必要ですが '{Current.Kind}' が来ました");
      // Missing token (span is Current's to keep location)
      return new Token(kind, "", null, Current.Span);
    }

    private void SkipBadTokens()
    {
      while (Current.Kind == TokenKind.BadToken)
        NextToken();
    }

    public CompilationUnitSyntax ParseCompilationUnit()
    {
      SkipBadTokens();

      var decls = new List<TopLevelDeclSyntax>();

      while (Current.Kind != TokenKind.EOF)
      {
        SkipBadTokens();

        if (Current.Kind == TokenKind.EOF)
          break;

        var start = Current.Span;

        TopLevelDeclSyntax? decl = Current.Kind switch
        {
          TokenKind.On => ParseOnDecl(),
          TokenKind.Func => ParseFuncDecl(),
          _ => null
        };

        if (decl is null)
        {
          Diagnostics.ReportError(Current.Span, $"トップレベルで '{Current.Kind}' は使えません（on / func を期待）");
          SynchronizeTopLevel();
          continue;
        }

        decls.Add(decl);

        // safety: ensure progress
        if (Current.Span.Start == start.Start && Current.Kind != TokenKind.EOF)
          NextToken();
      }

      var eof = Match(TokenKind.EOF);
      var span = decls.Count > 0 ? SpanUtil.Combine(decls[0].Span, eof.Span) : eof.Span;
      return new CompilationUnitSyntax(decls, eof, span);
    }

    private OnDeclSyntax ParseOnDecl()
    {
      var onKw = Match(TokenKind.On);

      // ここは「on PlayerJoined」みたいなDSL名にして、BinderでOnPlayerJoinedへマップする想定
      var eventName = Match(TokenKind.Identifier);
      var colon = Match(TokenKind.Colon);

      var body = ParseBlockRequired("on の本体にはブロック '{ ... }' が必要です");

      var span = SpanUtil.Combine(onKw.Span, body.Span);
      return new OnDeclSyntax(onKw, eventName, colon, body, span);
    }

    private FuncDeclSyntax ParseFuncDecl()
    {
      var funcKw = Match(TokenKind.Func);
      var name = Match(TokenKind.Identifier);
      var lParen = Match(TokenKind.LParen);

      var parameters = new List<ParamSyntax>();
      if (Current.Kind != TokenKind.RParen)
      {
        while (true)
        {
          var pStart = Current.Span;
          var pName = Match(TokenKind.Identifier);
          var pColon = Match(TokenKind.Colon);
          var pType = Match(TokenKind.Identifier);

          var pSpan = SpanUtil.Combine(pStart, pType.Span);
          parameters.Add(new ParamSyntax(pName, pColon, pType, pSpan));

          if (TryConsume(TokenKind.Comma, out _))
            continue;

          break;
        }
      }

      var rParen = Match(TokenKind.RParen);
      var colon = Match(TokenKind.Colon);

      var body = ParseBlockRequired("func の本体にはブロック '{ ... }' が必要です");

      var span = SpanUtil.Combine(funcKw.Span, body.Span);
      return new FuncDeclSyntax(funcKw, name, lParen, parameters, rParen, colon, body, span);
    }

    private BlockSyntax ParseBlockRequired(string errorMessage)
    {
      if (Current.Kind != TokenKind.LBrace)
      {
        Diagnostics.ReportError(Current.Span, errorMessage);
        // 回復：次の '{' か宣言開始まで飛ばして空ブロックを作る
        SynchronizeTo(TokenKind.LBrace, TokenKind.On, TokenKind.Func, TokenKind.EOF);
        if (Current.Kind != TokenKind.LBrace)
        {
          // どうにもならないので空ブロック
          var fakeL = new Token(TokenKind.LBrace, "{", null, Current.Span);
          var fakeR = new Token(TokenKind.RBrace, "}", null, Current.Span);
          return new BlockSyntax(fakeL, Array.Empty<StatementSyntax>(), fakeR, Current.Span);
        }
      }

      return ParseBlock();
    }

    private BlockSyntax ParseBlock()
    {
      var lBrace = Match(TokenKind.LBrace);
      var statements = new List<StatementSyntax>();

      while (Current.Kind != TokenKind.EOF && Current.Kind != TokenKind.RBrace)
      {
        SkipBadTokens();
        if (Current.Kind == TokenKind.EOF || Current.Kind == TokenKind.RBrace)
          break;

        var stmt = ParseStatement();
        statements.Add(stmt);

        // safety: ensure progress
        if (Current.Kind != TokenKind.EOF && stmt.Span.Length == 0)
          NextToken();
      }

      var rBrace = Match(TokenKind.RBrace);
      var span = SpanUtil.Combine(lBrace.Span, rBrace.Span);
      return new BlockSyntax(lBrace, statements, rBrace, span);
    }

    private StatementSyntax ParseStatement()
    {
      return Current.Kind switch
      {
        TokenKind.LBrace => ParseBlock(),
        TokenKind.Let => ParseLetStatement(),
        TokenKind.If => ParseIfStatement(),
        TokenKind.While => ParseWhileStatement(),
        TokenKind.Return => ParseReturnStatement(),
        _ => ParseExpressionStatement(),
      };
    }

    private LetStatementSyntax ParseLetStatement()
    {
      var letKw = Match(TokenKind.Let);
      var name = Match(TokenKind.Identifier);

      Token? colon = null;
      Token? typeName = null;
      if (TryConsume(TokenKind.Colon, out var c))
      {
        colon = c;
        typeName = Match(TokenKind.Identifier);
      }

      Token? eq = null;
      ExpressionSyntax? init = null;
      if (TryConsume(TokenKind.Equal, out var e))
      {
        eq = e;
        init = ParseExpression();
      }

      Token? semi = null;
      TryConsume(TokenKind.Semicolon, out var s);
      if (s is not null && s.Kind == TokenKind.Semicolon) semi = s;

      var endSpan = semi?.Span ?? init?.Span ?? typeName?.Span ?? name.Span;
      var span = SpanUtil.Combine(letKw.Span, endSpan);
      return new LetStatementSyntax(letKw, name, colon, typeName, eq, init, semi, span);
    }

    private IfStatementSyntax ParseIfStatement()
    {
      var ifKw = Match(TokenKind.If);

      // 仕様：if (cond) でも if cond でもどちらでもOKにしやすい
      // まずは if ( ... ) を推奨しつつ、無ければそのまま式を読む
      ExpressionSyntax condition;
      if (TryConsume(TokenKind.LParen, out var lp))
      {
        condition = ParseExpression();
        var rp = Match(TokenKind.RParen);
        condition = new ParenthesizedExpressionSyntax(lp, condition, rp, SpanUtil.Combine(lp.Span, rp.Span));
      }
      else
      {
        condition = ParseExpression();
      }

      var thenStmt = ParseStatement();

      Token? elseKw = null;
      StatementSyntax? elseStmt = null;
      if (TryConsume(TokenKind.Else, out var e))
      {
        elseKw = e;
        elseStmt = ParseStatement();
      }

      var endSpan = elseStmt?.Span ?? thenStmt.Span;
      var span = SpanUtil.Combine(ifKw.Span, endSpan);
      return new IfStatementSyntax(ifKw, condition, thenStmt, elseKw, elseStmt, span);
    }

    private WhileStatementSyntax ParseWhileStatement()
    {
      var whileKw = Match(TokenKind.While);

      ExpressionSyntax condition;
      if (TryConsume(TokenKind.LParen, out var lp))
      {
        condition = ParseExpression();
        var rp = Match(TokenKind.RParen);
        condition = new ParenthesizedExpressionSyntax(lp, condition, rp, SpanUtil.Combine(lp.Span, rp.Span));
      }
      else
      {
        condition = ParseExpression();
      }

      var body = ParseStatement();
      var span = SpanUtil.Combine(whileKw.Span, body.Span);
      return new WhileStatementSyntax(whileKw, condition, body, span);
    }

    private ReturnStatementSyntax ParseReturnStatement()
    {
      var ret = Match(TokenKind.Return);

      ExpressionSyntax? expr = null;
      // return; のために、明らかな終端なら式を読まない
      if (Current.Kind != TokenKind.Semicolon &&
          Current.Kind != TokenKind.RBrace &&
          Current.Kind != TokenKind.EOF)
      {
        expr = ParseExpression();
      }

      Token? semi = null;
      TryConsume(TokenKind.Semicolon, out var s);
      if (s is not null && s.Kind == TokenKind.Semicolon) semi = s;

      var endSpan = (semi?.Span ?? expr?.Span ?? ret.Span);
      var span = SpanUtil.Combine(ret.Span, endSpan);
      return new ReturnStatementSyntax(ret, expr, semi, span);
    }

    private ExpressionStatementSyntax ParseExpressionStatement()
    {
      var expr = ParseExpression();

      Token? semi = null;
      TryConsume(TokenKind.Semicolon, out var s);
      if (s is not null && s.Kind == TokenKind.Semicolon) semi = s;

      var span = semi is null ? expr.Span : SpanUtil.Combine(expr.Span, semi.Span);
      return new ExpressionStatementSyntax(expr, semi, span);
    }

    // ----------------------------
    // Expressions (Pratt)
    // ----------------------------

    private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
      SkipBadTokens();

      // assignment (right-associative): identifier '=' expr
      // これをPrattに混ぜるのが一番事故らない
      if (Current.Kind == TokenKind.Identifier && Peek(1).Kind == TokenKind.Equal)
      {
        var id = NextToken();
        var eq = NextToken();
        var rhs = ParseExpression(parentPrecedence: 1); // assignment lowest-ish
        var spanA = SpanUtil.Combine(id.Span, rhs.Span);
        return new AssignmentExpressionSyntax(id, eq, rhs, spanA);
      }

      var left = ParsePrefix();

      while (true)
      {
        SkipBadTokens();
        var precedence = GetBinaryPrecedence(Current.Kind);
        if (precedence == 0 || precedence <= parentPrecedence)
          break;

        var op = NextToken();
        var right = ParseExpression(precedence);
        var span = SpanUtil.Combine(left.Span, right.Span);
        left = new BinaryExpressionSyntax(left, op, right, span);
      }

      return left;
    }

    private ExpressionSyntax ParsePrefix()
    {
      SkipBadTokens();

      var start = Current.Span;

      // unary
      var unaryPrec = GetUnaryPrecedence(Current.Kind);
      if (unaryPrec > 0)
      {
        var op = NextToken();
        var operand = ParseExpression(unaryPrec);
        var span = SpanUtil.Combine(op.Span, operand.Span);
        return new UnaryExpressionSyntax(op, operand, span);
      }

      // primary
      ExpressionSyntax expr = Current.Kind switch
      {
        TokenKind.Number => new LiteralExpressionSyntax(NextToken(), start),
        TokenKind.String => new LiteralExpressionSyntax(NextToken(), start),
        TokenKind.True => new LiteralExpressionSyntax(NextToken(), start),
        TokenKind.False => new LiteralExpressionSyntax(NextToken(), start),

        TokenKind.Identifier => new NameExpressionSyntax(NextToken(), start),

        TokenKind.LParen => ParseParenExpression(),

        _ => ParseErrorExpression("式が必要です"),
      };

      // postfix: call chaining (foo()(x) まで許す)
      while (Current.Kind == TokenKind.LParen)
      {
        var lParen = NextToken();
        var args = new List<ExpressionSyntax>();

        if (Current.Kind != TokenKind.RParen)
        {
          while (true)
          {
            args.Add(ParseExpression());
            if (TryConsume(TokenKind.Comma, out _))
              continue;
            break;
          }
        }

        var rParen = Match(TokenKind.RParen);
        var span = SpanUtil.Combine(expr.Span, rParen.Span);
        expr = new CallExpressionSyntax(expr, lParen, args, rParen, span);
      }

      return expr;
    }

    private ExpressionSyntax ParseParenExpression()
    {
      var l = Match(TokenKind.LParen);
      var expr = ParseExpression();
      var r = Match(TokenKind.RParen);
      var span = SpanUtil.Combine(l.Span, r.Span);
      return new ParenthesizedExpressionSyntax(l, expr, r, span);
    }

    private ExpressionSyntax ParseErrorExpression(string message)
    {
      Diagnostics.ReportError(Current.Span, message + $"（'{Current.Kind}' は式として開始できません）");
      var bad = NextToken(); // progress
      var span = bad.Span;
      return new ErrorExpressionSyntax(bad, span);
    }

    private static int GetUnaryPrecedence(TokenKind kind) => kind switch
    {
      TokenKind.Bang => 30,
      TokenKind.Minus => 30,
      _ => 0
    };

    private static int GetBinaryPrecedence(TokenKind kind) => kind switch
    {
      TokenKind.Star => 20,
      TokenKind.Slash => 20,

      TokenKind.Plus => 10,
      TokenKind.Minus => 10,

      TokenKind.Less => 7,
      TokenKind.LessEqual => 7,
      TokenKind.Greater => 7,
      TokenKind.GreaterEqual => 7,

      TokenKind.EqualEqual => 5,
      TokenKind.BangEqual => 5,

      TokenKind.AndAnd => 3,
      TokenKind.OrOr => 2,

      _ => 0
    };

    // ----------------------------
    // Error recovery
    // ----------------------------

    private void SynchronizeTopLevel()
    {
      // 次の宣言っぽい位置まで読み飛ばす
      while (Current.Kind != TokenKind.EOF)
      {
        if (Current.Kind == TokenKind.On || Current.Kind == TokenKind.Func)
          return;

        NextToken();
      }
    }

    private void SynchronizeTo(params TokenKind[] kinds)
    {
      while (Current.Kind != TokenKind.EOF)
      {
        foreach (var k in kinds)
          if (Current.Kind == k) return;

        NextToken();
      }
    }
  }
}
