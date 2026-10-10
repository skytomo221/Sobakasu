using System;
using System.IO;
using Newtonsoft.Json;

namespace Skytomo221.Sobakasu.Compiler.Target.UdonApiCatalog
{
    /// <summary>Reads the persisted Udon API catalog without loading Unity or CLR API types.</summary>
    public static class UdonApiCatalogReader
    {
        public static UdonApiCatalogData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("The Udon API catalog is empty.");
            var data = JsonConvert.DeserializeObject<UdonApiCatalogData>(json);
            if (data == null)
                throw new InvalidDataException("The Udon API catalog is empty.");
            return data;
        }

        public static UdonApiCatalogData Read(TextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            return Parse(reader.ReadToEnd());
        }
    }
}
