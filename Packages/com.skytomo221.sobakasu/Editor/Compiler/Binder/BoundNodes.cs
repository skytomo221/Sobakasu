using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal sealed class TypeSymbol
  {
    public static readonly TypeSymbol Error = new("error");
    public static readonly TypeSymbol Void = new("void");
    public static readonly TypeSymbol String = new("string");
    public static readonly TypeSymbol Debug = new("Debug");
    public static readonly TypeSymbol Method = new("method");

    public string Name { get; }

    private TypeSymbol(string name)
    {
      Name = name;
    }
  }

  internal sealed class MethodSymbol
  {
    public string Name { get; }
    public IReadOnlyList<TypeSymbol> Parameters { get; }
    public TypeSymbol ReturnType { get; }
    public string ExternSignature { get; }

    public MethodSymbol(
        string name,
        IReadOnlyList<TypeSymbol> parameters,
        TypeSymbol returnType,
        string externSignature)
    {
      Name = name;
      Parameters = parameters;
      ReturnType = returnType;
      ExternSignature = externSignature;
    }
  }

  internal abstract class BoundNode
  {
  }

  internal abstract class BoundStatement : BoundNode
  {
  }

  internal abstract class BoundExpression : BoundNode
  {
    public abstract TypeSymbol Type { get; }
  }

  internal sealed class BoundErrorExpression : BoundExpression
  {
    public static readonly BoundErrorExpression Instance = new();

    public override TypeSymbol Type => TypeSymbol.Error;

    private BoundErrorExpression()
    {
    }
  }

  internal sealed class BoundProgram : BoundNode
  {
    public IReadOnlyList<BoundEventDeclaration> Events { get; }

    public BoundProgram(IReadOnlyList<BoundEventDeclaration> events)
    {
      Events = events;
    }
  }

  internal sealed class BoundEventDeclaration : BoundNode
  {
    public string Name { get; }
    public string ExportName { get; }
    public BoundBlockStatement Body { get; }

    public BoundEventDeclaration(
        string name,
        string exportName,
        BoundBlockStatement body)
    {
      Name = name;
      ExportName = exportName;
      Body = body;
    }
  }

  internal sealed class BoundBlockStatement : BoundStatement
  {
    public IReadOnlyList<BoundStatement> Statements { get; }

    public BoundBlockStatement(IReadOnlyList<BoundStatement> statements)
    {
      Statements = statements;
    }
  }

  internal sealed class BoundExpressionStatement : BoundStatement
  {
    public BoundExpression Expression { get; }

    public BoundExpressionStatement(BoundExpression expression)
    {
      Expression = expression;
    }
  }

  internal sealed class BoundNameExpression : BoundExpression
  {
    public string Name { get; }
    public override TypeSymbol Type { get; }

    public BoundNameExpression(string name, TypeSymbol type)
    {
      Name = name;
      Type = type;
    }
  }

  internal sealed class BoundStringLiteralExpression : BoundExpression
  {
    public string Value { get; }
    public override TypeSymbol Type => TypeSymbol.String;

    public BoundStringLiteralExpression(string value)
    {
      Value = value;
    }
  }

  internal sealed class BoundMemberAccessExpression : BoundExpression
  {
    public BoundExpression Receiver { get; }
    public string MemberName { get; }
    public MethodSymbol Method { get; }
    public override TypeSymbol Type { get; }

    public BoundMemberAccessExpression(
        BoundExpression receiver,
        string memberName,
        MethodSymbol method,
        TypeSymbol type)
    {
      Receiver = receiver;
      MemberName = memberName;
      Method = method;
      Type = type;
    }
  }

  internal sealed class BoundCallExpression : BoundExpression
  {
    public BoundExpression Target { get; }
    public IReadOnlyList<BoundExpression> Arguments { get; }
    public MethodSymbol Method { get; }
    public override TypeSymbol Type { get; }

    public BoundCallExpression(
        BoundExpression target,
        IReadOnlyList<BoundExpression> arguments,
        MethodSymbol method,
        TypeSymbol type)
    {
      Target = target;
      Arguments = arguments;
      Method = method;
      Type = type;
    }
  }
}
