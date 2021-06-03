#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AnvilPacker.Level;
using AnvilPacker.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace AnvilPacker.Encoder.Transforms
{
    public partial class TransformPipe
    {
        public static Dictionary<string, Type> KnownTransforms { get; } = new() {
            { "hidden_block_removal",       typeof(HiddenBlockRemovalTransform) },
            { "predict_upgrade_data",       typeof(UpgradeDataTransform)        },
        };

        //Example: "hidden_block_removal{samples=64,radius=3,cum_freqs=false,whitelist=['stone',dirt,4]},predict_upgrade_data"
        //Syntax is similar to JSON5
        //Strings are delimited by either " or '
        //TODO: Allow whitespace
        //TODO: Presets
        //TODO: Allow mutating presets, e.g. remove specific transforms and update values of existing ones.
        private static readonly Parser<char, TransformPipe> _parser = CreateParser();

        public static JsonSerializer SettingSerializer { get; } = CreateSettingSerializer();

        private static Parser<char, TransformPipe> CreateParser()
        {
            var LBrace = Char('{');
            var RBrace = Char('}');
            var LBracket = Char('[');
            var RBracket = Char(']');
            var Comma = Char(',');
            var Equal = Char('=');
            var Not = Char('!');

            var Identifier =
                Token(c => c is
                    (>= 'a' and <= 'z') or
                    (>= 'A' and <= 'Z') or
                    (>= '0' and <= '9') or
                    '_' or '-'
                )
                .ManyString()
                .Labelled("identifier");


            char[] EscapeCodes = { 'n',   'r',  't', '\"', '\'', '\\' };
            char[] EscapeChars = { '\n', '\r', '\t', '\"', '\'', '\\' };

            var EscapedCharacter =
                OneOf(EscapeCodes)
                    .Select(v => EscapeChars[System.Array.IndexOf(EscapeCodes, v)])
                    .Labelled("escape character");

            var QuotedString =
                Char('\\')
                    .Then(EscapedCharacter)
                    .Or(Token(c => c is not '\"' or '\''))
                    .ManyString()
                    .Between(OneOf('\"', '\''))
                    .Labelled("string literal");

            Parser<char, JToken> Value = null!;

            var Member =
                Identifier
                    .Before(Equal)
                    .Then(Rec(() => Value!), (k, v) => (k, v));

            var Object =
                Member
                    .Separated(Comma)
                    .Between(LBrace, RBrace)
                    .Select(e => {
                        var obj = new JObject();
                        foreach (var (key, val) in e) {
                            obj.Add(key, val);
                        }
                        return (JToken)obj;
                    });

            var Array =
                Rec(() => Value)
                    .Separated(Comma)
                    .Between(LBracket, RBracket)
                    .Select(e => {
                        var arr = new JArray();
                        foreach (var val in e) {
                            arr.Add(val);
                        }
                        return (JToken)arr;
                    });

            Value =
                OneOf(
                    Object,
                    Array,
                    Real.Select(v => (JToken)new JValue(v)),
                    QuotedString.Or(Identifier).Select(v => (JToken)new JValue(v)),
                    String("true").Select(v => (JToken)new JValue(true)),
                    String("false").Select(v => (JToken)new JValue(false)),
                    String("null").Select(v => (JToken)JValue.CreateNull())
                )
                .Labelled("value");

            var Transform = 
                Identifier
                    .Then(Object.Optional(), (name, settings) => CreateTransform(name, settings.GetValueOrDefault()));

            var Pipe =
                Transform
                    .Separated(Comma)
                    .Select(e => new TransformPipe(e));

            return Pipe;
        }
        private static JsonSerializer CreateSettingSerializer()
        {
            var serializerSettings = new JsonSerializerSettings();
            serializerSettings.Converters.Add(new BlockConverter());
            serializerSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            return JsonSerializer.CreateDefault(serializerSettings);
        }

        public static TransformPipe Parse(string str)
        {
            var result = _parser.Parse(str);
            if (!result.Success) {
                var error = result.Error!;
                var pos = error.ErrorPos;
                int index = GetIndex(str, error.ErrorPos);

                var sb = new StringBuilder();
                sb.Append($"Failed to parse transform pipe string. {error.Message}\n");

                bool hasExpected = error.Expected.Any();
                bool hasUnexpected = error.Unexpected.HasValue;
                if (hasExpected) {
                    sb.Append("Expected ");
                    int i = 0;
                    foreach (var exp in error.Expected) {
                        if (i++ != 0) sb.Append(", ");

                        if (exp.Label != null) {
                            sb.Append($"`{exp.Label}`");
                        } else {
                            sb.Append("`");
                            sb.AppendJoin("`, `", exp.Tokens!);
                            sb.Append("`");
                        }
                    }
                }
                if (hasUnexpected) {
                    sb.Append(hasExpected ? ", got `" : "Unexpected `");
                    sb.AppendJoin("`, `", error.Unexpected.Value);
                    sb.Append("`");
                }
                if (hasExpected || hasUnexpected) {
                    sb.Append('\n');
                }
                sb.Append($"at line {pos.Line}, col {pos.Col}:\n\n");

                int dispCol = Math.Min(pos.Col - 1, 30);
                var lineStart = Math.Max(index - dispCol, 0);
                var lineEnd = Math.Min(index + dispCol, str.Length);
                sb.Append($"`{str[lineStart..lineEnd]}`\n");
                sb.Append($"`{new string(' ', dispCol)}^ here`\n");

                throw new FormatException(sb.ToString());
            }
            return result.Value;
        }
        private static int GetIndex(string str, SourcePos pos)
        {
            //https://github.com/benjamin-hodgson/Pidgin/blob/7f7f2b4164720bfd5690e0420f58cc27a605285b/Pidgin/ParseState.ComputeSourcePos.cs#L53
            int line = 1;
            int col = 1;
            int i = 0;

            for (; i < str.Length && line <= pos.Line && col < pos.Col; i++) {
                char ch = str[i];
                if (ch == '\n') {
                    line++;
                } else {
                    col += (ch == '\t' ? 4 : 1);
                }
            }
            return i;
        }

        public static TransformBase CreateTransform(string name, JToken? settings)
        {
            if (!KnownTransforms.TryGetValue(name, out var type)) {
                throw new InvalidOperationException($"Unknown transform `{name}`");
            }
            var transform = (TransformBase)Activator.CreateInstance(type)!;

            if (settings != null) {
                SettingSerializer.Populate(settings.CreateReader(), transform);
            }
            return transform;
        }

        public static string GetTransformName(TransformBase transform)
        {
            return KnownTransforms.First(e => e.Value == transform.GetType()).Key;
        }

        private class BlockConverter : JsonConverter<Block>
        {
            public override Block? ReadJson(JsonReader reader, Type objectType, Block? existingValue, bool hasExistingValue, JsonSerializer serializer)
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
}