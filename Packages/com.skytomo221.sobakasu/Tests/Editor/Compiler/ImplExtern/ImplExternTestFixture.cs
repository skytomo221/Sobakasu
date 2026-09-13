namespace Skytomo221.Sobakasu.Tests.Editor
{
    public abstract class ImplExternTestFixture : SobakasuAssetCleanupFixture
    {
        protected const string MaybeDefinition = @"
lang ""maybe""
enum Maybe<T> {
  Nothing,
  Just(T),
}
";
        protected const string ProjectedTryGetSignature =
            "TestApi.__TryGet__TestOwnerRef__SystemBoolean";
        protected const string ProjectedMixedSignature =
            "TestApi.__Mixed__SystemInt32Ref_TestOwnerRef_SystemStringRef__SystemInt32";
        protected const string ProjectedValiditySignature =
            "VRCSDKBaseUtilities.__IsValid__TestOwner__SystemBoolean";
        protected const string ProjectedConstructorMaybeSignature =
            "TestFoo.__ctor__TestOwnerRef__TestFoo";
        protected const string ExternAbiBindingsSource = @"
fn ref_only(value: i32) -> i32
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.RefOnly(
      ref i32 value);
fn out_only() -> i32
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.OutOnly(
      out i32 value);
fn return_and_out() -> (bool, i32)
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.ReturnAndOut(
      out i32 value);
fn mixed(normal: i32, value: i32, flag: bool)
    -> (i32, i32, string, bool)
  = extern Skytomo221.Sobakasu.Tests.Editor.SobakasuExternAbiFixture.Mixed(
      i32 normal, ref i32 value, out string text, ref bool flag);
";

        protected SobakasuProgramAsset CreateProgramAsset()
        {
            return CreateImportedProgramAsset("SobakasuImplExternTests");
        }
    }
}
