using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sporefront.Data.Serialization
{
    public static class SporefrontJsonSettings
    {
        private static JsonSerializerSettings _compact;
        private static JsonSerializerSettings _indented;

        public static JsonSerializerSettings Compact =>
            _compact ?? (_compact = Create(Formatting.None));

        public static JsonSerializerSettings Indented =>
            _indented ?? (_indented = Create(Formatting.Indented));

        private static JsonSerializerSettings Create(Formatting formatting)
        {
            return new JsonSerializerSettings
            {
                Formatting = formatting,
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.None,
                Converters = new List<JsonConverter>
                {
                    new HexCoordinateConverter(),
                    new HexCoordinateKeyConverter()
                }
            };
        }
    }
}
