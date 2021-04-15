using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Level
{
    public class MBlockPalette
    {
        private List<MBlockState> _stateById = new(16);
        private Dictionary<int, int> _idByState = new(16);

        public int Count => _stateById.Count;

        public int GetOrAdd(MBlockState state)
        {
            if (_idByState.TryGetValue(state.Id, out int id)) {
                return id;
            }
            return Register(state);

            int Register(MBlockState state)
            {
                int id = _stateById.Count;
                _stateById.Add(state);
                _idByState.Add(state.Id, id);

                return id;
            }
        }

        public MBlockState Get(int id)
        {
            //depends on implicit out of bounds check, and throw if unitialized
            return _stateById[id] ?? throw new IndexOutOfRangeException();
        }
        public int GetId(MBlockState block)
        {
            if (_idByState.TryGetValue(block.Id, out int id)) {
                return id;
            }
            return -1;
        }
    }
}
