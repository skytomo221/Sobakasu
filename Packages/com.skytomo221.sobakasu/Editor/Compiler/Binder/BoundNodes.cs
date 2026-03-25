using System;
using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
  internal enum TypeKind
  {
    Error,
    U0,
    I32,
    U32,
    F32,
    String,
    Bool,
    Null,
    Debug,
    Method,
    Array
  }

  internal sealed class TypeSymbol : IEquatable<TypeSymbol>
  {
    public static readonly TypeSymbol Error = new(TypeKind.Error, "error");
    public static readonly TypeSymbol U0 = new(TypeKind.U0, "u0");
    public static readonly TypeSymbol Void = U0;
    public static readonly TypeSymbol I32 = new(TypeKind.I32, "i32");
    public static readonly TypeSymbol U32 = new(TypeKind.U32, "u32");
    public static readonly TypeSymbol F32 = new(TypeKind.F32, "f32");
    public static readonly TypeSymbol String = new(TypeKind.String, "string");
    public static readonly TypeSymbol Bool = new(TypeKind.Bool, "bool");
    public static readonly TypeSymbol Null = new(TypeKind.Null, "null");
    public static readonly TypeSymbol Debug = new(TypeKind.Debug, "Debug");
    public static readonly TypeSymbol Method = new(TypeKind.Method, "method");

    public TypeKind Kind { get; }
    public string Name { get; }
    public TypeSymbol ElementType { get; }
    public bool IsReferenceType => Kind == TypeKind.String || Kind == TypeKind.Array;

    private TypeSymbol(TypeKind kind, string name, TypeSymbol elementType = null)
    {
      Kind = kind;
      Name = name;
      ElementType = elementType;
    }

    public static TypeSymbol Array(TypeSymbol elementType)
    {
      if (elementType == null)
        throw new ArgumentNullException(nameof(elementType));

      return new TypeSymbol(TypeKind.Array, $"{elementType.Name}[]", elementType);
    }

    public bool Equals(TypeSymbol other)
    {
      if (ReferenceEquals(this, other))
        return true;

      if (other is null || Kind != other.Kind)
        return false;

      if (Kind != TypeKind.Array)
        return true;

      return Equals(ElementType, other.ElementType);
    }

    public override bool Equals(object obj)
    {
      return obj is TypeSymbol other && Equals(other);
    }

    public override int GetHashCode()
    {
      unchecked
      {
        return ((int)Kind * 397) ^ (ElementType?.GetHashCode() ?? 0);
      }
    }

    public static bool operator ==(TypeSymbol left, TypeSymbol right)
    {
      return Equals(left, right);
    }

    public static bool operator !=(TypeSymbol left, TypeSymbol right)
    {
      return !Equals(left, right);
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

  internal sealed class BoundInt32LiteralExpression : BoundExpression
  {
    public int Value { get; }
    public override TypeSymbol Type => TypeSymbol.I32;

    public BoundInt32LiteralExpression(int value)
    {
      Value = value;
    }
  }

  internal sealed class BoundUInt32LiteralExpression : BoundExpression
  {
    public uint Value { get; }
    public override TypeSymbol Type => TypeSymbol.U32;

    public BoundUInt32LiteralExpression(uint value)
    {
      Value = value;
    }
  }

  internal sealed class BoundFloat32LiteralExpression : BoundExpression
  {
    public float Value { get; }
    public override TypeSymbol Type => TypeSymbol.F32;

    public BoundFloat32LiteralExpression(float value)
    {
      Value = value;
    }
  }

  internal sealed class BoundBooleanLiteralExpression : BoundExpression
  {
    public bool Value { get; }
    public override TypeSymbol Type => TypeSymbol.Bool;

    public BoundBooleanLiteralExpression(bool value)
    {
      Value = value;
    }
  }

  internal sealed class BoundNullLiteralExpression : BoundExpression
  {
    public static readonly BoundNullLiteralExpression Instance = new();

    public override TypeSymbol Type => TypeSymbol.Null;

    private BoundNullLiteralExpression()
    {
    }
  }

  internal sealed class BoundArrayLiteralExpression : BoundExpression
  {
    public IReadOnlyList<BoundExpression> Elements { get; }
    public TypeSymbol ElementType { get; }
    public override TypeSymbol Type { get; }

    public BoundArrayLiteralExpression(
        IReadOnlyList<BoundExpression> elements,
        TypeSymbol elementType)
    {
      Elements = elements;
      ElementType = elementType;
      Type = TypeSymbol.Array(elementType);
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
