using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnvilPacker.Level
{
    public class BlockPalette
    {
        private List<BlockState> _stateById;
        private Dictionary<int, int> _idByState;

        public int Count => _stateById.Count;

        public BlockPalette(int initialCapacity = 16)
        {
            _stateById = new(initialCapacity);
            _idByState = new(initialCapacity);
        }

        public int Add(BlockState state)
        {
            int id = _stateById.Count;
            _idByState.Add(state.Id, id);
            _stateById.Add(state);

            return id;
        }
        public int GetOrAddId(BlockState state)
        {
            if (TryGetId(state, out int id)) {
                return id;
            }
            return Add(state);
        }

        public BlockState GetState(int id)
        {
            return _stateById[id];
        }

        public int GetId(BlockState state)
        {
            return _idByState[state.Id];
        }
        public bool TryGetId(BlockState state, out int id)
        {
            return _idByState.TryGetValue(state.Id, out id);
        }
    }
}
