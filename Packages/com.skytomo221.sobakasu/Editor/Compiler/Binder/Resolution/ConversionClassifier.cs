using System;
using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Diagnostic;
using Skytomo221.Sobakasu.Compiler.Parser;
using Skytomo221.Sobakasu.Compiler.Modules;
using Skytomo221.Sobakasu.Compiler.Semantics.Events;
using Skytomo221.Sobakasu.Compiler.Syntax;
using Skytomo221.Sobakasu.Compiler.Text;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class ConversionClassifier : BinderComponent
    {
        internal ConversionClassifier(BindingSession session) : base(session)
        {
        }

        internal TypeSymbol InferArrayElementType(IReadOnlyList<BoundExpression> elements)
        {
            TypeSymbol inferredType = null;
            foreach (var element in elements)
            {
                if (element.Type == TypeSymbol.Error)
                    continue;
                if (inferredType == null)
                {
                    inferredType = element.Type;
                    continue;
                }

                if (Session.ConversionClassifier.TryGetCommonElementType(inferredType, element.Type, out var commonType))
                    inferredType = commonType;
            }

            return inferredType;
        }

        internal bool TryGetCommonElementType(TypeSymbol left, TypeSymbol right, out TypeSymbol commonType)
        {
            if (left == right)
            {
                commonType = left;
                return true;
            }

            if (Session.ConversionClassifier.CanAssign(left, right))
            {
                commonType = left;
                return true;
            }

            if (Session.ConversionClassifier.CanAssign(right, left))
            {
                commonType = right;
                return true;
            }

            commonType = null;
            return false;
        }

        internal bool CanAssign(TypeSymbol targetType, TypeSymbol sourceType)
        {
            return Session.ConversionClassifier.TryGetConversionDistance(targetType, sourceType, out _);
        }

        internal bool CanAssignToLocal(TypeSymbol targetType, TypeSymbol sourceType)
        {
            if (targetType == TypeSymbol.Error || sourceType == TypeSymbol.Error)
                return true;
            if (sourceType == TypeSymbol.Never)
                return true;
            if (targetType == sourceType)
                return true;
            return Session.ConversionClassifier.IsImplicitObjectBoxingConversion(targetType, sourceType);
        }

        internal bool IsImplicitObjectBoxingConversion(TypeSymbol targetType, TypeSymbol sourceType)
        {
            if (targetType != TypeSymbol.Object || sourceType == null)
                return false;
            if (sourceType.UsesFlattenedAggregateStorage)
                return false;
            return sourceType.TypeKind is TypeKind.Bool or TypeKind.Char or TypeKind.I8 or TypeKind.U8 or TypeKind.I16 or TypeKind.U16 or TypeKind.I32 or TypeKind.U32 or TypeKind.I64 or TypeKind.U64 or TypeKind.F32 or TypeKind.F64 or TypeKind.String or TypeKind.Named;
        }

        internal bool TryGetConversionDistance(TypeSymbol targetType, TypeSymbol sourceType, out int distance)
        {
            if (targetType == TypeSymbol.Error || sourceType == TypeSymbol.Error)
            {
                distance = 0;
                return true;
            }

            if (sourceType == TypeSymbol.Never)
            {
                distance = 0;
                return true;
            }

            if (targetType == sourceType)
            {
                distance = 0;
                return true;
            }

            return Session.ConversionClassifier.TryGetNumericWideningDistance(targetType, sourceType, out distance);
        }

        internal bool TryGetNumericWideningDistance(TypeSymbol targetType, TypeSymbol sourceType, out int distance)
        {
            distance = 0;
            if (!Session.ConversionClassifier.TryGetNumericCategoryAndRank(targetType, out var targetCategory, out var targetRank) || !Session.ConversionClassifier.TryGetNumericCategoryAndRank(sourceType, out var sourceCategory, out var sourceRank))
            {
                return false;
            }

            if (targetCategory != sourceCategory || sourceRank > targetRank)
                return false;
            distance = targetRank - sourceRank;
            return true;
        }

        internal bool TryGetNumericCategoryAndRank(TypeSymbol type, out NumericCategory category, out int rank)
        {
            switch (type.TypeKind)
            {
                case TypeKind.I8:
                    category = NumericCategory.SignedInteger;
                    rank = 0;
                    return true;
                case TypeKind.I16:
                    category = NumericCategory.SignedInteger;
                    rank = 1;
                    return true;
                case TypeKind.I32:
                    category = NumericCategory.SignedInteger;
                    rank = 2;
                    return true;
                case TypeKind.I64:
                    category = NumericCategory.SignedInteger;
                    rank = 3;
                    return true;
                case TypeKind.U8:
                    category = NumericCategory.UnsignedInteger;
                    rank = 0;
                    return true;
                case TypeKind.U16:
                    category = NumericCategory.UnsignedInteger;
                    rank = 1;
                    return true;
                case TypeKind.U32:
                    category = NumericCategory.UnsignedInteger;
                    rank = 2;
                    return true;
                case TypeKind.U64:
                    category = NumericCategory.UnsignedInteger;
                    rank = 3;
                    return true;
                case TypeKind.F32:
                    category = NumericCategory.FloatingPoint;
                    rank = 0;
                    return true;
                case TypeKind.F64:
                    category = NumericCategory.FloatingPoint;
                    rank = 1;
                    return true;
                default:
                    category = default;
                    rank = -1;
                    return false;
            }
        }
    }
}
