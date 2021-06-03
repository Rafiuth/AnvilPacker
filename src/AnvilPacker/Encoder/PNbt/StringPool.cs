using System;
using System.Collections.Generic;
using System.Linq;
using AnvilPacker.Data;
using AnvilPacker.Util;

namespace AnvilPacker.Encoder.PNbt
{
    public class StringPool
    {
        private Dictionary<string, int> _freq = new();
        private Dictionary<string, int> _indices = null;
        private string[] _values = null;

        public void Add(string str)
        {
            _freq.TryGetValue(str, out int freq);
            _freq[str] = freq + 1;
        }
        public void Clear()
        {
            _freq.Clear();
            _indices = null;
            _values = null;
        }

        public void WriteTable(DataWriter dw)
        {
            var sorted = _freq.Where(v => v.Value > 1)
                              .OrderByDescending(v => v.Value)
                              .Take(256) //arbitrary limit
                              .ToList();

            _indices = new Dictionary<string, int>(sorted.Count);

            dw.WriteVarUInt(sorted.Count);
            foreach (var (str, freq) in sorted) {
                dw.WriteString(str, CodecPrimitives.WriteVarUInt);

                _indices[str] = _indices.Count;
            }
        }
        public void ReadTable(DataReader dr)
        {
            int len = dr.ReadVarUInt();
            _values = new string[len];
            for (int i = 0; i < len; i++) {
                _values[i] = dr.ReadString(dr.ReadVarUInt());
            }
        }

        public void Write(DataWriter dw, string str)
        {
            if (_indices.TryGetValue(str, out int index)) {
                dw.WriteVarUInt(index << 1 | 1);
                return;
            }
            dw.WriteString(str, (w, len) => w.WriteVarUInt(len << 1 | 0));
        }
        public string Read(DataReader dr)
        {
            int len = dr.ReadVarUInt();
            if ((len & 1) != 0) {
                return _values[len >> 1];
            }
            return dr.ReadString(len >> 1);
        }
    }
}