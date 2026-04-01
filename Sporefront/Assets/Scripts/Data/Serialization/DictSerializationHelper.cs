using System;
using System.Collections.Generic;

namespace Sporefront.Data
{
    /// <summary>
    /// Helpers for serializing Dictionary&lt;TEnum, int&gt; to parallel arrays
    /// (required because Unity's JsonUtility cannot serialize dictionaries).
    /// </summary>
    public static class DictSerializationHelper
    {
        public static void SerializeEnumDict<TKey>(Dictionary<TKey, int> dict, out string[] keys, out int[] values)
            where TKey : struct
        {
            if (dict == null || dict.Count == 0)
            {
                keys = Array.Empty<string>();
                values = Array.Empty<int>();
                return;
            }
            var keyList = new List<string>(dict.Count);
            var valueList = new List<int>(dict.Count);
            foreach (var kvp in dict)
            {
                keyList.Add(kvp.Key.ToString());
                valueList.Add(kvp.Value);
            }
            keys = keyList.ToArray();
            values = valueList.ToArray();
        }

        public static Dictionary<TKey, int> DeserializeEnumDict<TKey>(string[] keys, int[] values)
            where TKey : struct
        {
            var dict = new Dictionary<TKey, int>();
            if (keys == null || values == null || keys.Length != values.Length)
                return dict;
            for (int i = 0; i < keys.Length; i++)
            {
                if (Enum.TryParse<TKey>(keys[i], out var enumVal))
                    dict[enumVal] = values[i];
            }
            return dict;
        }
    }
}
