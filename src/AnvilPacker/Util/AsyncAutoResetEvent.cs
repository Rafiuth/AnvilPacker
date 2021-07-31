using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnvilPacker.Util
{
    //https://stackoverflow.com/questions/32654509/awaitable-autoresetevent
    //https://devblogs.microsoft.com/pfxteam/building-async-coordination-primitives-part-2-asyncautoresetevent/
    //https://github.com/dotnet/runtime/issues/35962
    public class AsyncAutoResetEvent
    {
        private readonly Queue<TaskCompletionSource> _waits = new();
        private bool _signaled;

        public AsyncAutoResetEvent(bool initialState)
        {
            _signaled = initialState;
        }

        public Task WaitAsync()
        {
            lock (_waits) {
                if (_signaled) {
                    _signaled = false;
                    return Task.CompletedTask;
                } else {
                    var tcs = new TaskCompletionSource();
                    _waits.Enqueue(tcs);
                    return tcs.Task;
                }
            }
        }

        public void Set()
        {
            TaskCompletionSource toRelease = null;

            lock (_waits) {
                if (!_waits.TryDequeue(out toRelease)) {
                    _signaled = true;
                }
            }
            toRelease?.SetResult();
        }
    }
}