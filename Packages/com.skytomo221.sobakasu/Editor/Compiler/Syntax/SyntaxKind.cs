namespace Skytomo221.Sobakasu.Compiler.Syntax
{
    public enum SyntaxKind
    {
        On,
        UseKeyword,
        ModKeyword,
        AsKeyword,
        FnKeyword,
        ReceiveKeyword,
        SendKeyword,
        ToKeyword,
        LangKeyword,
        StructKeyword,
        EnumKeyword,
        ImplKeyword,
        ExternKeyword,
        StaticKeyword,
        SelfKeyword,
        SelfTypeKeyword,
        NewKeyword,
        PubKeyword,
        SyncKeyword,
        ConstKeyword,
        StateKeyword,
        LetKeyword,
        MutKeyword,
        ReturnKeyword,
        MatchKeyword,
        IfKeyword,
        ElseKeyword,
        WhileKeyword,
        LoopKeyword,
        BreakKeyword,
        ContinueKeyword,
        RedoKeyword,
        RefKeyword,
        OutKeyword,
        TrueKeyword,
        FalseKeyword,
        Identifier,
        Int8Literal,
        UInt8Literal,
        Int16Literal,
        UInt16Literal,
        Int32Literal,
        UInt32Literal,
        Int64Literal,
        UInt64Literal,
        Float32Literal,
        Float64Literal,
        CharacterLiteral,
        LabelIdentifier,
        String,
        PlusToken,
        MinusToken,
        StarToken,
        SlashToken,
        PercentToken,
        EqualsEqualsToken,
        BangEqualsToken,
        LessToken,
        LessOrEqualsToken,
        GreaterToken,
        GreaterOrEqualsToken,
        BangToken,
        QuestionToken,
        AtToken,
        AmpersandAmpersandToken,
        PipePipeToken,
        TildeToken,
        AmpersandToken,
        PipeToken,
        CaretToken,
        LessLessToken,
        GreaterGreaterToken,
        Dot,
        Comma,
        Colon,
        ArrowToken,
        FatArrowToken,
        EqualsToken,
        PlusEqualsToken,
        MinusEqualsToken,
        StarEqualsToken,
        SlashEqualsToken,
        PercentEqualsToken,
        AmpersandEqualsToken,
        PipeEqualsToken,
        CaretEqualsToken,
        LessLessEqualsToken,
        GreaterGreaterEqualsToken,
        LeftBrace,
        RightBrace,
        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        Semicolon,
        EndOfFile,
        BadToken
    }

    internal static class SyntaxFacts
    {
        public static int GetUnaryOperatorPrecedence(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.BangToken or SyntaxKind.TildeToken => 12,
                _ => 0,
            };
        }

        public static int GetBinaryOperatorPrecedence(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.StarToken or SyntaxKind.SlashToken or SyntaxKind.PercentToken => 11,
                SyntaxKind.PlusToken or SyntaxKind.MinusToken => 10,
                SyntaxKind.LessLessToken or SyntaxKind.GreaterGreaterToken => 9,
                SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or SyntaxKind.GreaterToken or SyntaxKind.GreaterOrEqualsToken => 8,
                SyntaxKind.EqualsEqualsToken or SyntaxKind.BangEqualsToken => 7,
                SyntaxKind.AmpersandToken => 6,
                SyntaxKind.CaretToken => 5,
                SyntaxKind.PipeToken => 4,
                SyntaxKind.AmpersandAmpersandToken => 3,
                SyntaxKind.PipePipeToken => 2,
                SyntaxKind.EqualsToken or SyntaxKind.PlusEqualsToken or SyntaxKind.MinusEqualsToken or SyntaxKind.StarEqualsToken or SyntaxKind.SlashEqualsToken or SyntaxKind.PercentEqualsToken or SyntaxKind.AmpersandEqualsToken or SyntaxKind.PipeEqualsToken or SyntaxKind.CaretEqualsToken or SyntaxKind.LessLessEqualsToken or SyntaxKind.GreaterGreaterEqualsToken => 1,
                _ => 0,
            };
        }

        public static bool IsAssignmentOperator(SyntaxKind kind)
        {
            return kind switch
            {
                SyntaxKind.EqualsToken or SyntaxKind.PlusEqualsToken or SyntaxKind.MinusEqualsToken or SyntaxKind.StarEqualsToken or SyntaxKind.SlashEqualsToken or SyntaxKind.PercentEqualsToken or SyntaxKind.AmpersandEqualsToken or SyntaxKind.PipeEqualsToken or SyntaxKind.CaretEqualsToken or SyntaxKind.LessLessEqualsToken or SyntaxKind.GreaterGreaterEqualsToken => true,
                _ => false,
            };
        }

        public static bool IsRightAssociative(SyntaxKind kind)
        {
            return IsAssignmentOperator(kind);
        }
    }
}
