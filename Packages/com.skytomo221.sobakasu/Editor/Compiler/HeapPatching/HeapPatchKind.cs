namespace Skytomo221.Sobakasu.Compiler
{
    public enum HeapPatchKind
    {
        Constant,
        GlobalInitializer,
        FieldInitializer,
        ArrayInitializer,
        UserDefinedValue
    }
}
