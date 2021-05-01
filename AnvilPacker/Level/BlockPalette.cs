using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnvilPacker.Util;

namespace AnvilPacker.Level
{
    public class BlockPalette : IEnumerable<BlockState>
    {
        private List<BlockState> _stateById;
        private DictionarySlim<int, BlockId> _idByState;

        public int Count => _stateById.Count;

        public BlockPalette(int initialCapacity = 16)
        {
            _stateById = new(initialCapacity);
            _idByState = new(initialCapacity);
        }

        public BlockId Add(BlockState state)
        {
            var id = (BlockId)_stateById.Count;
            _idByState.Add(state.Id, id);
            _stateById.Add(state);
            
            return id;
        }
        public BlockId GetOrAddId(BlockState state)
        {
            if (TryGetId(state, out BlockId id)) {
                return id;
            }
            return Add(state);
        }

        public BlockState GetState(BlockId id)
        {
            return _stateById[id];
        }

        public BlockId GetId(BlockState state)
        {
            return TryGetId(state, out BlockId id) 
                    ? id 
                    : throw new KeyNotFoundException("Block not in palette");
        }
        public bool TryGetId(BlockState state, out BlockId id)
        {
            return _idByState.TryGetValue(state.Id, out id);
        }

        public IEnumerator<BlockState> GetEnumerator() => _stateById.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
