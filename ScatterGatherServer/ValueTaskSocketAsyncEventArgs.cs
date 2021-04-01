using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks.Sources;

namespace ScatterGatherServer
{
    // The only other way to get gathered writes returns a Task, and allocations are impacting numbers.
    // This is here to represent the numbers we'll get if we add a proper ValueTask-returning gather API to Stream.
    sealed class ValueTaskSocketAsyncEventArgs : SocketAsyncEventArgs, IValueTaskSource<int>
    {
        ManualResetValueTaskSourceCore<int> _taskSource;

        public ValueTask<int> Task =>
            new ValueTask<int>(this, _taskSource.Version);

        public void PrepareForOperation() =>
            _taskSource.Reset();

        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            if (SocketError == SocketError.Success)
            {
                _taskSource.SetResult(e.BytesTransferred);
            }
            else
            {
                _taskSource.SetException(new SocketException((int)SocketError));
            }
        }

        int IValueTaskSource<int>.GetResult(short token) =>
            _taskSource.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource<int>.GetStatus(short token) =>
            _taskSource.GetStatus(token);

        void IValueTaskSource<int>.OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _taskSource.OnCompleted(continuation, state, token, flags);
    }
}
