using System.Collections.Generic;

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    public abstract class MemberSyntax : SyntaxNode
    {
    }

    internal sealed class LanguageItemSyntax : SyntaxNode
    {
        public Syntax.SyntaxToken LangKeyword { get; }
        public Syntax.SyntaxToken Item { get; }

        public LanguageItemSyntax(
            Syntax.SyntaxToken langKeyword,
            Syntax.SyntaxToken item)
        {
            LangKeyword = langKeyword;
            Item = item;
        }
    }

    internal enum SynchronizationModeSyntaxKind
    {
        None,
        Linear,
        Smooth,
        Invalid
    }

    internal sealed class SynchronizationModifierSyntax : SyntaxNode
    {
        public Syntax.SyntaxToken SyncKeyword { get; }
        public Syntax.SyntaxToken OpenParenToken { get; }
        public Syntax.SyntaxToken ModeToken { get; }
        public Syntax.SyntaxToken CloseParenToken { get; }
        public SynchronizationModeSyntaxKind Mode { get; }

        public SynchronizationModifierSyntax(
            Syntax.SyntaxToken syncKeyword,
            Syntax.SyntaxToken openParenToken,
            Syntax.SyntaxToken modeToken,
            Syntax.SyntaxToken closeParenToken,
            SynchronizationModeSyntaxKind mode)
        {
            SyncKeyword = syncKeyword;
            OpenParenToken = openParenToken;
            ModeToken = modeToken;
            CloseParenToken = closeParenToken;
            Mode = mode;
        }
    }

    internal sealed class StateDeclarationSyntax : MemberSyntax
    {
        public Syntax.SyntaxToken PubKeyword { get; }
        public SynchronizationModifierSyntax SynchronizationModifier { get; }
        public Syntax.SyntaxToken StateKeyword { get; }
        public Syntax.SyntaxToken MutKeyword { get; }
        public Syntax.SyntaxToken Identifier { get; }
        public TypeClauseSyntax TypeClause { get; }
        public Syntax.SyntaxToken EqualsToken { get; }
        public ExpressionSyntax Initializer { get; }
        public Syntax.SyntaxToken SemicolonToken { get; }

        public StateDeclarationSyntax(
            Syntax.SyntaxToken pubKeyword,
            SynchronizationModifierSyntax synchronizationModifier,
            Syntax.SyntaxToken stateKeyword,
            Syntax.SyntaxToken mutKeyword,
            Syntax.SyntaxToken identifier,
            TypeClauseSyntax typeClause,
            Syntax.SyntaxToken equalsToken,
            ExpressionSyntax initializer,
            Syntax.SyntaxToken semicolonToken)
        {
            PubKeyword = pubKeyword;
            SynchronizationModifier = synchronizationModifier;
            StateKeyword = stateKeyword;
            MutKeyword = mutKeyword;
            Identifier = identifier;
            TypeClause = typeClause;
            EqualsToken = equalsToken;
            Initializer = initializer;
            SemicolonToken = semicolonToken;
        }
    }

    internal sealed class ImplDeclarationSyntax : MemberSyntax
    {
        public LanguageItemSyntax LanguageItem { get; }
        public Syntax.SyntaxToken PubKeyword { get; }
        public Syntax.SyntaxToken ImplKeyword { get; }
        public GenericParameterListSyntax GenericParameters { get; }
        public TypeSyntax TargetType { get; }
        public Syntax.SyntaxToken EqualsToken { get; }
        public Syntax.SyntaxToken ExternKeyword { get; }
        public QualifiedNameSyntax ExternalTypeName { get; }
        public Syntax.SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<FunctionDeclarationSyntax> Methods { get; }
        public Syntax.SyntaxToken CloseBraceToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public ImplDeclarationSyntax(
            LanguageItemSyntax languageItem,
            Syntax.SyntaxToken pubKeyword,
            Syntax.SyntaxToken implKeyword,
            GenericParameterListSyntax genericParameters,
            TypeSyntax targetType,
            Syntax.SyntaxToken equalsToken,
            Syntax.SyntaxToken externKeyword,
            QualifiedNameSyntax externalTypeName,
            Syntax.SyntaxToken openBraceToken,
            IReadOnlyList<FunctionDeclarationSyntax> methods,
            Syntax.SyntaxToken closeBraceToken)
        {
            LanguageItem = languageItem;
            PubKeyword = pubKeyword;
            ImplKeyword = implKeyword;
            GenericParameters = genericParameters;
            TargetType = targetType;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalTypeName = externalTypeName;
            OpenBraceToken = openBraceToken;
            Methods = methods;
            CloseBraceToken = closeBraceToken;
        }
    }
}

namespace Skytomo221.Sobakasu.Compiler.Parser
{
    using Skytomo221.Sobakasu.Compiler.Syntax;

    internal sealed class AggregateFieldDeclarationSyntax : SyntaxNode
    {
        public SyntaxToken Identifier { get; }
        public SyntaxToken ColonToken { get; }
        public TypeSyntax Type { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public SyntaxToken ExternalMemberName { get; }
        public SyntaxToken CommaToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public AggregateFieldDeclarationSyntax(
            SyntaxToken identifier,
            SyntaxToken colonToken,
            TypeSyntax type,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            SyntaxToken externalMemberName,
            SyntaxToken commaToken)
        {
            Identifier = identifier;
            ColonToken = colonToken;
            Type = type;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalMemberName = externalMemberName;
            CommaToken = commaToken;
        }
    }

    internal sealed class ConstDeclarationSyntax : MemberSyntax
    {
        public Syntax.SyntaxToken PubKeyword { get; }
        public SynchronizationModifierSyntax RejectedSynchronizationModifier { get; }
        public Syntax.SyntaxToken ConstKeyword { get; }
        public Syntax.SyntaxToken Identifier { get; }
        public TypeClauseSyntax TypeClause { get; }
        public Syntax.SyntaxToken EqualsToken { get; }
        public ExpressionSyntax Initializer { get; }
        public Syntax.SyntaxToken SemicolonToken { get; }

        public ConstDeclarationSyntax(
            Syntax.SyntaxToken pubKeyword,
            SynchronizationModifierSyntax rejectedSynchronizationModifier,
            Syntax.SyntaxToken constKeyword,
            Syntax.SyntaxToken identifier,
            TypeClauseSyntax typeClause,
            Syntax.SyntaxToken equalsToken,
            ExpressionSyntax initializer,
            Syntax.SyntaxToken semicolonToken)
        {
            PubKeyword = pubKeyword;
            RejectedSynchronizationModifier = rejectedSynchronizationModifier;
            ConstKeyword = constKeyword;
            Identifier = identifier;
            TypeClause = typeClause;
            EqualsToken = equalsToken;
            Initializer = initializer;
            SemicolonToken = semicolonToken;
        }
    }

    internal sealed class LegacyTopLevelLetDeclarationSyntax : MemberSyntax
    {
        public Syntax.SyntaxToken FirstToken { get; }
        public Syntax.SyntaxToken LetKeyword { get; }
        public Syntax.SyntaxToken SemicolonToken { get; }

        public LegacyTopLevelLetDeclarationSyntax(
            Syntax.SyntaxToken firstToken,
            Syntax.SyntaxToken letKeyword,
            Syntax.SyntaxToken semicolonToken)
        {
            FirstToken = firstToken;
            LetKeyword = letKeyword;
            SemicolonToken = semicolonToken;
        }
    }

    internal sealed class StructDeclarationSyntax : MemberSyntax
    {
        public LanguageItemSyntax LanguageItem { get; }
        public SyntaxToken PubKeyword { get; }
        public SyntaxToken StructKeyword { get; }
        public SyntaxToken Identifier { get; }
        public GenericParameterListSyntax GenericParameters { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public QualifiedNameSyntax ExternalTypeName { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<AggregateFieldDeclarationSyntax> Fields { get; }
        public SyntaxToken CloseBraceToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public StructDeclarationSyntax(
            LanguageItemSyntax languageItem,
            SyntaxToken pubKeyword,
            SyntaxToken structKeyword,
            SyntaxToken identifier,
            GenericParameterListSyntax genericParameters,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            QualifiedNameSyntax externalTypeName,
            SyntaxToken openBraceToken,
            IReadOnlyList<AggregateFieldDeclarationSyntax> fields,
            SyntaxToken closeBraceToken)
        {
            LanguageItem = languageItem;
            PubKeyword = pubKeyword;
            StructKeyword = structKeyword;
            Identifier = identifier;
            GenericParameters = genericParameters;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalTypeName = externalTypeName;
            OpenBraceToken = openBraceToken;
            Fields = fields;
            CloseBraceToken = closeBraceToken;
        }
    }

    internal enum EnumVariantSyntaxKind
    {
        Unit,
        Tuple,
        Struct
    }

    internal sealed class EnumVariantDeclarationSyntax : SyntaxNode
    {
        public SyntaxToken Identifier { get; }
        public EnumVariantSyntaxKind VariantKind { get; }
        public SyntaxToken OpenParenToken { get; }
        public IReadOnlyList<TypeSyntax> TuplePayloadTypes { get; }
        public IReadOnlyList<SyntaxToken> TupleSeparators { get; }
        public SyntaxToken CloseParenToken { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<AggregateFieldDeclarationSyntax> NamedPayloadFields { get; }
        public SyntaxToken CloseBraceToken { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public SyntaxToken ExternalMemberName { get; }
        public bool IsExternalBinding => EqualsToken != null;
        public SyntaxToken CommaToken { get; }

        public EnumVariantDeclarationSyntax(
            SyntaxToken identifier,
            EnumVariantSyntaxKind variantKind,
            SyntaxToken openParenToken,
            IReadOnlyList<TypeSyntax> tuplePayloadTypes,
            IReadOnlyList<SyntaxToken> tupleSeparators,
            SyntaxToken closeParenToken,
            SyntaxToken openBraceToken,
            IReadOnlyList<AggregateFieldDeclarationSyntax> namedPayloadFields,
            SyntaxToken closeBraceToken,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            SyntaxToken externalMemberName,
            SyntaxToken commaToken)
        {
            Identifier = identifier;
            VariantKind = variantKind;
            OpenParenToken = openParenToken;
            TuplePayloadTypes = tuplePayloadTypes;
            TupleSeparators = tupleSeparators;
            CloseParenToken = closeParenToken;
            OpenBraceToken = openBraceToken;
            NamedPayloadFields = namedPayloadFields;
            CloseBraceToken = closeBraceToken;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalMemberName = externalMemberName;
            CommaToken = commaToken;
        }
    }

    internal sealed class EnumDeclarationSyntax : MemberSyntax
    {
        public LanguageItemSyntax LanguageItem { get; }
        public SyntaxToken PubKeyword { get; }
        public SyntaxToken EnumKeyword { get; }
        public SyntaxToken Identifier { get; }
        public GenericParameterListSyntax GenericParameters { get; }
        public SyntaxToken EqualsToken { get; }
        public SyntaxToken ExternKeyword { get; }
        public QualifiedNameSyntax ExternalTypeName { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<EnumVariantDeclarationSyntax> Variants { get; }
        public SyntaxToken CloseBraceToken { get; }
        public bool IsExternalBinding => EqualsToken != null;

        public EnumDeclarationSyntax(
            LanguageItemSyntax languageItem,
            SyntaxToken pubKeyword,
            SyntaxToken enumKeyword,
            SyntaxToken identifier,
            GenericParameterListSyntax genericParameters,
            SyntaxToken equalsToken,
            SyntaxToken externKeyword,
            QualifiedNameSyntax externalTypeName,
            SyntaxToken openBraceToken,
            IReadOnlyList<EnumVariantDeclarationSyntax> variants,
            SyntaxToken closeBraceToken)
        {
            LanguageItem = languageItem;
            PubKeyword = pubKeyword;
            EnumKeyword = enumKeyword;
            Identifier = identifier;
            GenericParameters = genericParameters;
            EqualsToken = equalsToken;
            ExternKeyword = externKeyword;
            ExternalTypeName = externalTypeName;
            OpenBraceToken = openBraceToken;
            Variants = variants;
            CloseBraceToken = closeBraceToken;
        }
    }

    internal sealed class AggregateInitializerFieldSyntax : SyntaxNode
    {
        public SyntaxToken Identifier { get; }
        public SyntaxToken ColonToken { get; }
        public ExpressionSyntax Expression { get; }
        public SyntaxToken CommaToken { get; }

        public AggregateInitializerFieldSyntax(
            SyntaxToken identifier,
            SyntaxToken colonToken,
            ExpressionSyntax expression,
            SyntaxToken commaToken)
        {
            Identifier = identifier;
            ColonToken = colonToken;
            Expression = expression;
            CommaToken = commaToken;
        }
    }

    internal sealed class AggregateInitializerExpressionSyntax : ExpressionSyntax
    {
        public ExpressionSyntax Target { get; }
        public SyntaxToken OpenBraceToken { get; }
        public IReadOnlyList<AggregateInitializerFieldSyntax> Fields { get; }
        public SyntaxToken CloseBraceToken { get; }

        public AggregateInitializerExpressionSyntax(
            ExpressionSyntax target,
            SyntaxToken openBraceToken,
            IReadOnlyList<AggregateInitializerFieldSyntax> fields,
            SyntaxToken closeBraceToken)
        {
            Target = target;
            OpenBraceToken = openBraceToken;
            Fields = fields;
            CloseBraceToken = closeBraceToken;
        }
    }
}
