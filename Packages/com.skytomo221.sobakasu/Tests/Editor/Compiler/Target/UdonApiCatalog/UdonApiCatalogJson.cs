using Newtonsoft.Json;
using Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog;

namespace Skytomo221.Sobakasu.Tests.Editor
{
    internal static class UdonApiCatalogJson
    {
        public static string Serialize(UdonApiCatalogData data)
        {
            return JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                Converters = { new UdonApiTypeRefJsonConverter() }
            });
        }
    }
}
