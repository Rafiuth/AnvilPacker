using System.Globalization;

namespace AnvilPacker.Data.Nbt
{
    public class NbtPrinter
    {
        private TextWriter _tw;
        private int _level = 0;
        public bool Pretty = true;

        public NbtPrinter(TextWriter tw)
        {
            _tw = tw;
        }

        private void Begin(string brace)
        {
            _tw.Write(brace);
            _level++;
        }
        private void End(string brace, bool newLine)
        {
            _level--;
            if (newLine && Pretty) {
                _tw.Write('\n');
                Indent();
            }
            _tw.Write(brace);
        }
        private void Indent()
        {
            _tw.Write(new string(' ', _level * 2));
        }

        public void Print(NbtToken token)
        {
            switch (token.Type) {
                case TagType.Byte:      PrintPrim(token.AsByte(), "b"); break;
                case TagType.Short:     PrintPrim(token.AsShort(), "s"); break;
                case TagType.Int:       PrintPrim(token.AsInt(), ""); break;
                case TagType.Long:      PrintPrim(token.AsLong(), "L"); break;
                case TagType.Float:     PrintPrim(token.AsFloat(), "f"); break;
                case TagType.Double:    PrintPrim(token.AsDouble(), ""); break;
                case TagType.String:    PrintStr(token.AsString()); break;
                case TagType.ByteArray: PrintArray("byte", token.AsByteArray()); break;
                case TagType.IntArray:  PrintArray("int", token.AsIntArray()); break;
                case TagType.LongArray: PrintArray("long", token.AsLongArray()); break;
                case TagType.List: {
                    bool isPrim = token.ElemType.IsPrimitive();
                    PrintSequence("[", "]", isPrim, token.EnumerateElems(), Print);
                    break;
                }
                case TagType.Compound: {
                    PrintSequence("{", "}", false, token.EnumerateProps(), e => {
                        PrintStr(e.Key);
                        _tw.Write(Pretty ? ": " : ":");
                        Print(e.Value);
                    });
                    break;
                }
                default:
                    throw new NotImplementedException($"Printer for {token.Type} ({token.GetType()})");
            }
        }

        private void PrintStr(string str)
        {
            _tw.Write('"');
            _tw.Write(str.Replace("\"", "\\\""));
            _tw.Write('"');
        }
        private void PrintPrim(object val, string postfix)
        {
            string str = string.Format(CultureInfo.InvariantCulture, "{0}", val);

            _tw.Write(str);
            if (val is double or float && !str.Contains('.')) {
                _tw.Write(".0");
            }
            _tw.Write(postfix);
        }
        private void PrintSequence<T>(string openBrace, string closeBrace, bool isPrim, IEnumerable<T> elems, Action<T> printElem)
        {
            Begin(openBrace);

            int i = 0;
            foreach (var elem in elems) {
                _tw.Write(i == 0 ? "" : (Pretty ? ", " : ","));
                if (Pretty && (!isPrim || i % 32 == 0)) {
                    _tw.Write('\n');
                    Indent();
                }
                printElem(elem);
                i++;
            }
            End(closeBrace, i > 0);
        }
        private void PrintArray<T>(string type, NbtArrayView<T> arr) where T : unmanaged
        {
            PrintSequence(type + "[", "]", true, arr, v => _tw.Write(v));
        }
    }
}