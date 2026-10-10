using System.Collections.Generic;
using Skytomo221.Sobakasu.Compiler.Binder;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;
using Skytomo221.Sobakasu.Tools.StandardLibraryGenerator;
using Skytomo221.Sobakasu.Tools.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class UdonBindingGeneratorTestSupport
    {
        internal static UdonBindingGenerator CreateGenerator(UdonApiCatalogData catalog = null, UdonBindingGenerationConfig configuration = null)
        {
            catalog ??= CreateCatalogFixture();
            var json = UdonApiCatalogGenerator.Serialize(catalog);
            var source = UdonBindingSourceModel.FromCatalog(catalog);
            var environment = SobakasuCompilationEnvironment.FromUdonApiCatalogJson(json);
            return new UdonBindingGenerator(
                source,
                new SobakasuBindingRenderer(new UdonBindingTypeFormatter(source)),
                environment,
                configuration ?? UdonBindingGenerationConfig.CreateDefault());
        }

        internal static UdonApiCatalogData CreateCatalogFixture()
        {
            var catalog = new UdonApiCatalogData { formatVersion = 2 };
            catalog.types.Add(Type("System.String", "Reference", "System"));
            catalog.types.Add(Type("System.Boolean", "Value", "System"));
            catalog.types.Add(Type("System.Int32", "Value", "System"));
            catalog.types.Add(Type("System.Single", "Value", "System"));
            catalog.types.Add(Type("System.Void", "Value", "System"));
            catalog.types.Add(Type("System.Object", "Reference", "System"));
            catalog.types.Add(Type("System.Type", "Reference", "System"));
            catalog.types.Add(Type("Example.Widget", "Reference", "Example"));
            catalog.types.Add(Type("Example.Point", "Value", "Example"));
            catalog.types.Add(Type("Example.Mode", "Enum", "Example"));
            catalog.types.Add(Type("Example.Outer+Inner", "Reference", "Example"));
            catalog.types.Add(Type("Example.MathApi", "Reference", "Example", true));
            catalog.types.Add(Type("Example.BaseMetadata", "Reference", "Example"));
            catalog.unexposedClrTypeNames.Add("Example.BaseMetadata");
            catalog.types.Find(type => type.runtimeName == "Example.Mode").@enum = new UdonApiEnumRecord
            {
                underlyingType = Named("System.Int32"),
                constants = new List<UdonApiEnumConstantRecord>
                {
                    new() { name = "Idle", value = "0" },
                    new() { name = "Running", value = "1" }
                }
            };
            catalog.members.Add(Member("Example.Widget", "Example.Widget", "GetName", "InstanceMethod", "Method", false,
                "Example.Widget.GetName()", Named("System.String")));
            catalog.members.Add(Member("Example.Widget", "Example.Widget", "Name", "PropertyGetter", "Getter", false,
                "Example.Widget.Name.get", Named("System.String")));
            catalog.members.Add(Member("Example.Widget", "Example.Widget", "Name", "PropertySetter", "Setter", false,
                "Example.Widget.Name.set", Named("System.Void"), Parameter("value", Named("System.String"))));
            catalog.members.Add(Member("Example.Widget", "Example.Widget", "Count", "FieldGetter", "Getter", false,
                "Example.Widget.Count.get", Named("System.Int32")));
            catalog.members.Add(Member("Example.Widget", "Example.Widget", ".ctor", "Constructor", "Constructor", false,
                "Example.Widget..ctor()", Named("Example.Widget")));
            catalog.members.Add(Member("Example.Widget", "Example.Widget", "op_Addition", "StaticMethod", "Operator", true,
                "Example.Widget.op_Addition(Example.Widget,Example.Widget)", Named("Example.Widget"),
                Parameter("left", Named("Example.Widget")), Parameter("right", Named("Example.Widget"))));
            catalog.members.Add(Member("Example.Widget", "Example.Widget", "Transform", "InstanceMethod", "Method", false,
                "Example.Widget.Transform(System.Int32&,System.String&)", Named("System.Void"),
                Parameter("value", Named("System.Int32"), "Ref"), Parameter("label", Named("System.String"), "Out")));
            catalog.members.Add(Member("Example.Widget", "Example.Widget", "GetLabels", "InstanceMethod", "Method", false,
                "Example.Widget.GetLabels()", Array(Named("System.String"))));
            catalog.members.Add(Member("Example.MathApi", "Example.MathApi", "Clamp", "StaticMethod", "Method", true,
                "Example.MathApi.Clamp(System.Single)", Named("System.Single"), Parameter("value", Named("System.Single"))));
            catalog.members.Add(Member("Example.Point", "Example.Point", "x", "FieldGetter", "Getter", false,
                "Example.Point.x.get", Named("System.Single")));
            catalog.members.Add(Member("Example.Widget", "Example.Widget", "Echo", "InstanceMethod", "Method", false,
                "Example.Widget.Echo<T>(T)", GenericParameter(0),
                Parameter("T", Named("System.Type"), "GenericTypeArgument"), Parameter("value", GenericParameter(0))));
            catalog.members[catalog.members.Count - 1].genericParameters.Add(new UdonApiGenericParameterRecord { name = "T" });
            catalog.members.Add(Member("Example.Widget", "Example.Widget", "TryGet", "InstanceMethod", "Method", false,
                "Example.Widget.TryGet(System.String&)", Named("System.Boolean"), Parameter("value", Named("System.String"), "Out")));
            return catalog;
        }

        private static UdonApiTypeRecord Type(string runtimeName, string shape, string clrNamespace, bool staticContainer = false) =>
            new() { runtimeName = runtimeName, shape = shape, clrNamespace = clrNamespace, isStaticApiContainer = staticContainer };

        private static UdonApiMemberRecord Member(string host, string declaring, string name, string sourceKind, string kind, bool isStatic, string signature, ExternTypeRef returnType, params ExternParameterRecord[] parameters)
        {
            return new UdonApiMemberRecord
            {
                hostType = Named(host), clrDeclaringType = Named(declaring), name = name, sourceKind = sourceKind,
                kind = kind, origin = "Clr", isStatic = isStatic, clrSignature = signature,
                displaySignature = signature, externSignature = signature, abiReturnType = returnType,
                abiParameters = new List<ExternParameterRecord>(parameters)
            };
        }

        private static ExternParameterRecord Parameter(string name, ExternTypeRef type, string mode = "Normal") =>
            new() { name = name, type = type, passingMode = mode };

        private static ExternTypeRef Named(string runtimeName) => new() { kind = "Named", runtimeName = runtimeName };
        private static ExternTypeRef Array(ExternTypeRef element) => new() { kind = "Array", element = element };
        private static ExternTypeRef GenericParameter(int ordinal) => new() { kind = "GenericParameter", scope = "method:Example.Widget.Echo", ordinal = ordinal };
    }
}
