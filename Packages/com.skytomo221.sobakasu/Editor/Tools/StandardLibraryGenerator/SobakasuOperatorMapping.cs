using System;

namespace Skytomo221.Sobakasu.Tools.StandardLibraryGenerator
{
    internal static class SobakasuOperatorMapping
    {
        public static bool TryGet(string clrName, out string token, out bool isUnary)
        {
            isUnary = clrName == "op_UnaryPlus" || clrName == "op_UnaryNegation" || clrName == "op_LogicalNot" || clrName == "op_OnesComplement";
            token = clrName switch
            {
                "op_Addition" => "+", "op_Subtraction" => "-", "op_Multiply" => "*", "op_Division" => "/", "op_Modulus" => "%",
                "op_Equality" => "==", "op_Inequality" => "!=", "op_LessThan" => "<", "op_LessThanOrEqual" => "<=",
                "op_GreaterThan" => ">", "op_GreaterThanOrEqual" => ">=", "op_BitwiseAnd" => "&", "op_BitwiseOr" => "|",
                "op_ExclusiveOr" => "^", "op_LeftShift" => "<<", "op_RightShift" => ">>", "op_UnaryPlus" => "+",
                "op_UnaryNegation" => "-", "op_LogicalNot" => "!", "op_OnesComplement" => "~", _ => null
            };
            return token != null;
        }
        public static bool IsOperator(string clrName) => TryGet(clrName, out _, out _);
        public static bool TryGet(UdonBindingSourceMember member, out string token, out bool isUnary) => TryGet(member?.Name, out token, out isUnary);
        public static bool IsOperator(UdonBindingSourceMember member) => member != null && member.IsOperator;
    }
}
