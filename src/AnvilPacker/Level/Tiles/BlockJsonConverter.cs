using System;
using AnvilPacker.Util;
using Newtonsoft.Json;

namespace AnvilPacker.Level
{
    public class BlockJsonConverter : JsonConverter<Block>
    {
        public static BlockJsonConverter Instance { get; } = new();

        public override Block ReadJson(JsonReader reader, Type objectType, Block? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType) {
                case JsonToken.String: {
                    string name = (string)reader.Value!;
                    return BlockRegistry.GetBlock(name);
                }
                case JsonToken.Float:
                case JsonToken.Integer: {
                    int id = Convert.ToInt32(reader.Value!);
                    return BlockRegistry.GetLegacyState(id << 4).Block;
                }
                default: throw new InvalidOperationException($"Can't parse '{reader.TokenType}' to block");
            }
        }
        public override void WriteJson(JsonWriter writer, Block? value, JsonSerializer serializer)
        {
            Ensure.That(value != null);

            var state = value.DefaultState;
            if (state.HasAttrib(BlockAttributes.Legacy)) {
                writer.WriteValue(state.Id >> 4);
            } else {
                writer.WriteValue(state.ToString());
            }
        }
    }
}