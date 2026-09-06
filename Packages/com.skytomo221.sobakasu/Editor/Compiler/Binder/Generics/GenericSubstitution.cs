namespace Skytomo221.Sobakasu.Compiler.Binder
{
    internal sealed class GenericSubstitution
    {
        internal TypeSymbol Substitute(
            TypeSymbol type,
            System.Collections.Generic.IReadOnlyDictionary<TypeSymbol, TypeSymbol> substitutions)
        {
            return TypeSymbol.Substitute(type, substitutions);
        }
    }
}