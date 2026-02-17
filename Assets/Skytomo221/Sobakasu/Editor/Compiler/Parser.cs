using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler
{
  // ============================================================
  // AST
  // ============================================================

  public abstract class SyntaxNode
  {
    public TextSpan Span { get; protected set; }
    protected SyntaxNode(TextSpan span) => Span = span;
  }

  public sealed class CompilationUnitSyntax : SyntaxNode
  {
    public IReadOnlyList<TopLevelDeclSyntax> Declarations { get; }
    public Token EndOfFileToken { get; }

    public CompilationUnitSyntax(IReadOnlyList<TopLevelDeclSyntax> decls, Token eof, TextSpan span)
        : base(span)
    {
      Declarations = decls;
      EndOfFileToken = eof;
    }
  }

  public abstract class TopLevelDeclSyntax : SyntaxNode
  {
    protected TopLevelDeclSyntax(TextSpan span) : base(span) { }
  }

  public sealed class OnDeclSyntax : TopLevelDeclSyntax
  {
    public Token OnKeyword { get; }
    public Token EventName { get; }
    public Token ColonToken { get; }
    public BlockSyntax Body { get; }

    public OnDeclSyntax(Token onKw, Token eventName, Token colon, BlockSyntax body, TextSpan span)
        : base(span)
    {
      OnKeyword = onKw;
      EventName = eventName;
      ColonToken = colon;
      Body = body;
    }
  }

  public sealed class FuncDeclSyntax : TopLevelDeclSyntax
  {
    public Token FuncKeyword { get; }
    public Token Name { get; }
    public Token LParen { get; }
    public IReadOnlyList<ParamSyntax> Parameters { get; }
    public Token RParen { get; }
    public Token ColonToken { get; }
    public BlockSyntax Body { get; }

    public FuncDeclSyntax(Token funcKw, Token name, Token lParen, IReadOnlyList<ParamSyntax> parameters, Token rParen, Token colon, BlockSyntax body, TextSpan span)
        : base(span)
    {
      FuncKeyword = funcKw;
      Name = name;
      LParen = lParen;
      Parameters = parameters;
      RParen = rParen;
      ColonToken = colon;
      Body = body;
    }
  }

  public sealed class ParamSyntax : SyntaxNode
  {
    public Token Name { get; }
    public Token ColonToken { get; }
    public Token TypeName { get; }

    public ParamSyntax(Token name, Token colon, Token typeName, TextSpan span)
        : base(span)
    {
      Name = name;
      ColonToken = colon;
      TypeName = typeName;
    }
  }

  public abstract class StatementSyntax : SyntaxNode
  {
    protected StatementSyntax(TextSpan span) : base(span) { }
  }

  public sealed class BlockSyntax : StatementSyntax
  {
    public Token LBrace { get; }
    public IReadOnlyList<StatementSyntax> Statements { get; }
    public Token RBrace { get; }

    public BlockSyntax(Token lBrace, IReadOnlyList<StatementSyntax> statements, Token rBrace, TextSpan span)
        : base(span)
    {
      LBrace = lBrace;
      Statements = statements;
      RBrace = rBrace;
    }
  }

  public sealed class LetStatementSyntax : StatementSyntax
  {
    public Token LetKeyword { get; }
    public Token Name { get; }
    public Token? ColonToken { get; }
    public Token? TypeName { get; }
    public Token? EqualToken { get; }
    public ExpressionSyntax? Initializer { get; }
    public Token? Semicolon { get; }

    public LetStatementSyntax(Token letKw, Token name, Token? colon, Token? typeName, Token? equal, ExpressionSyntax? init, Token? semi, TextSpan span)
        : base(span)
    {
      LetKeyword = letKw;
      Name = name;
      ColonToken = colon;
      TypeName = typeName;
      EqualToken = equal;
      Initializer = init;
      Semicolon = semi;
    }
  }

  public sealed class IfStatementSyntax : StatementSyntax
  {
    public Token IfKeyword { get; }
    public ExpressionSyntax Condition { get; }
    public StatementSyntax Then { get; }
    public Token? ElseKeyword { get; }
    public StatementSyntax? Else { get; }

    public IfStatementSyntax(Token ifKw, ExpressionSyntax cond, StatementSyntax thenStmt, Token? elseKw, StatementSyntax? elseStmt, TextSpan span)
        : base(span)
    {
      IfKeyword = ifKw;
      Condition = cond;
      Then = thenStmt;
      ElseKeyword = elseKw;
      Else = elseStmt;
    }
  }

  public sealed class WhileStatementSyntax : StatementSyntax
  {
    public Token WhileKeyword { get; }
    public ExpressionSyntax Condition { get; }
    public StatementSyntax Body { get; }

    public WhileStatementSyntax(Token whileKw, ExpressionSyntax cond, StatementSyntax body, TextSpan span)
        : base(span)
    {
      WhileKeyword = whileKw;
      Condition = cond;
      Body = body;
    }
  }

  public sealed class ReturnStatementSyntax : StatementSyntax
  {
    public Token ReturnKeyword { get; }
    public ExpressionSyntax? Expression { get; }
    public Token? Semicolon { get; }

    public ReturnStatementSyntax(Token retKw, ExpressionSyntax? expr, Token? semi, TextSpan span)
        : base(span)
    {
      ReturnKeyword = retKw;
      Expression = expr;
      Semicolon = semi;
    }
  }

  public sealed class ExpressionStatementSyntax : StatementSyntax
  {
    public ExpressionSyntax Expression { get; }
    public Token? Semicolon { get; }

    public ExpressionStatementSyntax(ExpressionSyntax expr, Token? semi, TextSpan span)
        : base(span)
    {
      Expression = expr;
      Semicolon = semi;
    }
  }

  public abstract class ExpressionSyntax : SyntaxNode
  {
    protected ExpressionSyntax(TextSpan span) : base(span) { }
  }

  public sealed class NameExpressionSyntax : ExpressionSyntax
  {
    public Token Identifier { get; }
    public NameExpressionSyntax(Token id, TextSpan span) : base(span) => Identifier = id;
  }

  public sealed class LiteralExpressionSyntax : ExpressionSyntax
  {
    public Token LiteralToken { get; }
    public object? Value => LiteralToken.Value;

    public LiteralExpressionSyntax(Token lit, TextSpan span) : base(span) => LiteralToken = lit;
  }

  public sealed class UnaryExpressionSyntax : ExpressionSyntax
  {
    public Token OperatorToken { get; }
    public ExpressionSyntax Operand { get; }

    public UnaryExpressionSyntax(Token op, ExpressionSyntax operand, TextSpan span)
        : base(span)
    {
      OperatorToken = op;
      Operand = operand;
    }
  }

  public sealed class BinaryExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Left { get; }
    public Token OperatorToken { get; }
    public ExpressionSyntax Right { get; }

    public BinaryExpressionSyntax(ExpressionSyntax left, Token op, ExpressionSyntax right, TextSpan span)
        : base(span)
    {
      Left = left;
      OperatorToken = op;
      Right = right;
    }
  }

  public sealed class AssignmentExpressionSyntax : ExpressionSyntax
  {
    public Token Identifier { get; }
    public Token EqualToken { get; }
    public ExpressionSyntax Expression { get; }

    public AssignmentExpressionSyntax(Token id, Token eq, ExpressionSyntax expr, TextSpan span)
        : base(span)
    {
      Identifier = id;
      EqualToken = eq;
      Expression = expr;
    }
  }

  public sealed class CallExpressionSyntax : ExpressionSyntax
  {
    public ExpressionSyntax Callee { get; }
    public Token LParen { get; }
    public IReadOnlyList<ExpressionSyntax> Arguments { get; }
    public Token RParen { get; }

    public CallExpressionSyntax(ExpressionSyntax callee, Token lParen, IReadOnlyList<ExpressionSyntax> args, Token rParen, TextSpan span)
        : base(span)
    {
      Callee = callee;
      LParen = lParen;
      Arguments = args;
      RParen = rParen;
    }
  }

  public sealed class ParenthesizedExpressionSyntax : ExpressionSyntax
  {
    public Token LParen { get; }
    public ExpressionSyntax Expression { get; }
    public Token RParen { get; }

    public ParenthesizedExpressionSyntax(Token lParen, ExpressionSyntax expr, Token rParen, TextSpan span)
        : base(span)
    {
      LParen = lParen;
      Expression = expr;
      RParen = rParen;
    }
  }

  public sealed class ErrorExpressionSyntax : ExpressionSyntax
  {
    public Token BadToken { get; }
    public ErrorExpressionSyntax(Token bad, TextSpan span) : base(span) => BadToken = bad;
  }

  internal static class SpanUtil
  {
    public static TextSpan Combine(TextSpan a, TextSpan b)
    {
      int start = a.Start;
      int end = Math.Max(a.End, b.End);
      int length = Math.Max(0, end - start);
      return new TextSpan(start, length, a.Line, a.Column);
    }
  }

  // ============================================================
  // Parser (recursive descent + Pratt)
  // ============================================================

  public sealed class Parser
  {
    private readonly Token[] _tokens;
    private int _pos;

    public DiagnosticBag Diagnostics { get; }

    public Parser(string source)
    {
      Diagnostics = new DiagnosticBag();

      var tokens = new List<Token>(256);
      var lexer = new Lexer(source ?? "", Diagnostics);

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
