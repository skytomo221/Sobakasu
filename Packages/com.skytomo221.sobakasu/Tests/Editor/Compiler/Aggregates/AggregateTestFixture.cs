namespace Skytomo221.Sobakasu.Tests.Editor
{
    public abstract class AggregateTestFixture : SobakasuAssetCleanupFixture
    {
        protected const string IntArrayConstructor =
            "SystemInt32Array.__ctor__SystemInt32__SystemInt32Array";
        protected const string IntArrayGetter =
            "SystemInt32Array.__Get__SystemInt32__SystemInt32";
        protected const string IntArraySetter =
            "SystemInt32Array.__Set__SystemInt32_SystemInt32__SystemVoid";
        protected const string BoolArrayConstructor =
            "SystemBooleanArray.__ctor__SystemInt32__SystemBooleanArray";
        protected const string BoolArrayGetter =
            "SystemBooleanArray.__Get__SystemInt32__SystemBoolean";
        protected const string BoolArraySetter =
            "SystemBooleanArray.__Set__SystemInt32_SystemBoolean__SystemVoid";

        protected SobakasuProgramAsset CreateProgramAsset()
        {
            return CreateImportedProgramAsset("SobakasuAggregateTests");
        }
    }
}
