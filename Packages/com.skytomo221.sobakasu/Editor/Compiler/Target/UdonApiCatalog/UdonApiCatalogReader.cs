using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog
{
    /// <summary>
    /// Reads the persisted Udon API Catalog v2 contract without projecting it into
    /// compiler symbols. Consumers such as the compiler and Standard Library
    /// Generator must share this parser instead of maintaining separate JSON rules.
    /// </summary>
    public static class UdonApiCatalogReader
    {
        public static UdonApiCatalogData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("The Udon API catalog is empty.");

            JObject root;
            try
            {
                using var reader = new JsonTextReader(new StringReader(json));
                root = JObject.Load(
                    reader,
                    new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "The Udon API catalog is invalid JSON.",
                    exception);
            }

            ValidateJsonShape(root, null);

            try
            {
                var data = root.ToObject<UdonApiCatalogData>(
                    JsonSerializer.Create(
                        new JsonSerializerSettings
                        {
                            MissingMemberHandling = MissingMemberHandling.Error,
                            Converters = { new UdonApiTypeRefJsonConverter() }
                        }));
                if (data == null)
                    throw new InvalidDataException("The Udon API catalog is empty.");
                return data;
            }
            catch (Exception exception) when (
                exception is JsonException || exception is ArgumentException)
            {
                throw new InvalidDataException(
                    "The Udon API catalog has an invalid v2 record.",
                    exception);
            }
        }

        public static UdonApiCatalogData Read(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            return Parse(reader.ReadToEnd());
        }

        private static void ValidateJsonShape(JToken token, string propertyName)
        {
            if (token.Type == JTokenType.Null)
                throw new InvalidDataException(
                    "Null is prohibited in the Udon API catalog.");

            if (token is JArray array)
            {
                if (array.Count == 0)
                    throw new InvalidDataException(
                        "Empty arrays are prohibited in the Udon API catalog.");
                foreach (var item in array)
                    ValidateJsonShape(item, propertyName);
                return;
            }

            if (token is not JObject obj)
                return;

            if (!obj.Properties().Any() && propertyName != "constructors")
                throw new InvalidDataException(
                    "Empty objects are prohibited in the Udon API catalog.");

            foreach (var property in obj.Properties())
            {
                if (property.Value.Type == JTokenType.Boolean &&
                    !(bool)property.Value &&
                    (property.Name == "abstract" ||
                     property.Name == "sealed" ||
                     property.Name == "hasPublicParameterlessConstructor" ||
                     property.Name == "referenceType" ||
                     property.Name == "nonNullableValueType" ||
                     property.Name == "defaultConstructor" ||
                     property.Name == "params"))
                {
                    throw new InvalidDataException(
                        $"Optional boolean '{property.Name}' must be omitted when false.");
                }

                ValidateJsonShape(property.Value, property.Name);
            }
        }
    }
}
