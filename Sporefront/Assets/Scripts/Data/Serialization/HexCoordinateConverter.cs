using System;
using Newtonsoft.Json;
using Sporefront.Models;

namespace Sporefront.Data.Serialization
{
    /// <summary>
    /// Newtonsoft JsonConverter for HexCoordinate.
    /// Serializes as "q,r" string so it can be used as a Dictionary key in JSON.
    /// </summary>
    public class HexCoordinateConverter : JsonConverter<HexCoordinate>
    {
        public override void WriteJson(JsonWriter writer, HexCoordinate value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.q},{value.r}");
        }

        public override HexCoordinate ReadJson(JsonReader reader, Type objectType,
            HexCoordinate existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string s = reader.Value as string;
            if (string.IsNullOrEmpty(s)) return default;

            string[] parts = s.Split(',');
            if (parts.Length != 2) return default;

            if (int.TryParse(parts[0], out int q) && int.TryParse(parts[1], out int r))
                return new HexCoordinate(q, r);

            return default;
        }
    }

    /// <summary>
    /// Converter for HexCoordinate when used as a Dictionary key.
    /// </summary>
    public class HexCoordinateKeyConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(HexCoordinate);
        }

        public override object ReadJson(JsonReader reader, Type objectType,
            object existingValue, JsonSerializer serializer)
        {
            string s = reader.Value as string;
            if (string.IsNullOrEmpty(s)) return default(HexCoordinate);

            string[] parts = s.Split(',');
            if (parts.Length != 2) return default(HexCoordinate);

            if (int.TryParse(parts[0], out int q) && int.TryParse(parts[1], out int r))
                return new HexCoordinate(q, r);

            return default(HexCoordinate);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var hex = (HexCoordinate)value;
            writer.WritePropertyName($"{hex.q},{hex.r}");
        }
    }
}
