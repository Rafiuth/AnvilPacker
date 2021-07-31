using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Pidgin;
using static Pidgin.Parser;
using static Pidgin.Parser<char>;

namespace AnvilPacker.Util
{
    //TODO: Allow whitespace
    public class SettingParser
    {
        private readonly Dictionary<string, Type> _types;
        private readonly Type _rootType;
        private readonly JsonSerializer _serializer;
        private readonly Parser<char, JToken> _parser;

        public SettingParser(Type rootType, IEnumerable<(string Name, Type Type)> types, IEnumerable<JsonConverter> converters = null)
            : this(rootType, types.Select(v => new KeyValuePair<string, Type>(v.Name, v.Type)), converters)
        {

        }
        public SettingParser(Type rootType, IEnumerable<KeyValuePair<string, Type>> types, IEnumerable<JsonConverter> converters = null)
        {
            _types = new Dictionary<string, Type>(types);
            _rootType = rootType;
            _parser = GetParser(rootType);

            var ss = new JsonSerializerSettings() {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                MissingMemberHandling = MissingMemberHandling.Error,
                ContractResolver = new DefaultContractResolver() {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };
            ss.Converters.Add(new TypeBinderConverter(_types));
            if (converters != null) {
                foreach (var converter in converters) {
                    ss.Converters.Add(converter);
                }
            }
            _serializer = JsonSerializer.CreateDefault(ss);
        }

        private static readonly Parser<char, JToken> ValueParser, RootedObjParser, ArrayParser;

        static SettingParser()
        {
            var Comma = Char(',');

            var Identifier =
                Token(c => c is
                    (>= 'a' and <= 'z') or
                    (>= 'A' and <= 'Z') or
                    (>= '0' and <= '9') or
                    '_' or '-'
                )
                .ManyString()
                .Labelled("identifier");

            char[] EscapeCodes = {  'n',  'r',  't', '\"', '\'', '\\' };
            char[] EscapeChars = { '\n', '\r', '\t', '\"', '\'', '\\' };

            var EscapedCharacter =
                Char('\\').Then(
                    OneOf(EscapeCodes)
                        .Select(v => EscapeChars[System.Array.IndexOf(EscapeCodes, v)])
                        .Labelled("escape character")
                );

            var QuotedString =
                EscapedCharacter
                .Or(AnyCharExcept('\"', '\''))
                .ManyString()
                .Between(OneOf('\"', '\''))
                .Labelled("string");

            var Value = default(Parser<char, JToken>)!;

            var Property =
                Identifier
                    .Before(Char('='))
                    .Then(Rec(() => Value), (k, v) => (k, v))
                    .Labelled("property");

            Func<IEnumerable<(string, JToken)>, JToken> CreateObject = e => {
                var obj = new JObject();
                foreach (var (key, val) in e) {
                    obj.Add(key, val);
                }
                return (JToken)obj;
            };
            Func<IEnumerable<JToken>, JToken> CreateArray = e => {
                var arr = new JArray();
                foreach (var val in e) {
                    arr.Add(val);
                }
                return (JToken)arr;
            };

            var Object =
                Property
                    .Separated(Comma)
                    .Between(Char('{'), Char('}'))
                    .Select(CreateObject)
                    .Labelled("object");

            var Array =
                Rec(() => Value)
                    .Separated(Comma)
                    .Between(Char('['), Char(']'))
                    .Select(CreateArray)
                    .Labelled("array");

            var UnquotedStringOrTypedObject =
                Identifier
                    .Then(Object.Optional(), (id, obj) => {
                        if (!obj.HasValue) {
                            return new JValue(id);
                        }
                        obj.Value["$type"] = id;
                        return obj.Value;
                    });;

            Value =
                OneOf(
                    Object,
                    Array,
                    Real.Select(v => (JToken)new JValue(v)),
                    Try(String("true")).Select(v => (JToken)new JValue(true)),
                    Try(String("false")).Select(v => (JToken)new JValue(false)),
                    Try(String("null")).Select(v => (JToken)JValue.CreateNull()),
                    QuotedString.Select(v => (JToken)new JValue(v)),
                    UnquotedStringOrTypedObject
                )
                .Labelled("value");

            RootedObjParser = Property.Separated(Comma).Select(CreateObject);
            ArrayParser = Value.Separated(Comma).Select(CreateArray);
            ValueParser = Value;
        }
        private Parser<char, JToken> GetParser(Type rootType)
        {
            if (typeof(IEnumerable).IsAssignableFrom(rootType)) {
                return ArrayParser;
            }
            if (rootType != null) {
                return RootedObjParser;
            }
            return ValueParser;
        }

        public T Parse<T>(string str)
        {
            //FIXME: will silently fail when _rootType != null && str has braces like `{prop=x}`
            var json = ParseRaw(str);
            return (T)json.ToObject(_rootType ?? typeof(T), _serializer)!;
        }

        private JToken ParseRaw(string str)
        {
            var result = _parser.Parse(str);
            if (!result.Success) {
                var error = result.Error!;
                var pos = error.ErrorPos;
                int index = GetIndex(str, error.ErrorPos);

                var sb = new StringBuilder();
                sb.Append($"Failed to parse settings. {error.Message}\n");

                bool hasExpected = error.Expected.Any();
                bool hasUnexpected = error.Unexpected.HasValue;
                if (hasExpected) {
                    sb.Append("Expected ");
                    int i = 0;
                    foreach (var exp in error.Expected) {
                        if (i++ != 0) sb.Append(", ");

                        if (exp.Label != null) {
                            sb.Append(exp.Label);
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
                    col = 1;
                } else {
                    col += (ch == '\t' ? 4 : 1);
                }
            }
            return i;
        }

        private class TypeBinderConverter : JsonConverter
        {
            private Dictionary<string, Type> _types;

            public TypeBinderConverter(Dictionary<string, Type> types)
            {
                _types = types;
            }

            public override bool CanConvert(Type objectType)
            {
                foreach (var type in _types.Values) {
                    if (objectType.IsAssignableFrom(type)) {
                        return true;
                    }
                }
                return false;
            }
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                var token = JToken.Load(reader);

                var typeName = (token as JObject)?["$type"] ?? (token as JValue);
                if (typeName != null && typeName.Type == JTokenType.String) {
                    objectType = _types[typeName.Value<string>()];
                }
                var obj = Activator.CreateInstance(objectType);
                if (token is JObject json) {
                    json.Remove("$type");
                    serializer.Populate(json.CreateReader(), obj);
                }
                return obj;
            }
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }
        }
    }
}