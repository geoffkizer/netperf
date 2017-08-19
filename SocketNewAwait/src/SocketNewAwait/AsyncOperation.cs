using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Threading;

namespace SocketNewAwait
{
    // TODO Move all this
    public static class AsyncOperationHelper
    {
        private class AcceptOperation : AsyncOperation<Socket>
        {
            private SocketAsyncEventArgs _e;

            public AcceptOperation()
            {
                _e = new SocketAsyncEventArgs();
                _e.Completed += OnCompleted;
            }

            private void OnCompleted(object sender, SocketAsyncEventArgs e)
            {
                Debug.Assert(e == _e);

                CompleteAsynchronously(_e.AcceptSocket);
            }

            public AsyncOperationTask<Socket> Run(Socket listenSocket)
            {
                Start();

                bool pending = listenSocket.AcceptAsync(_e);
                if (!pending)
                {
                    CompleteSynchronously(_e.AcceptSocket);
                }

                return new AsyncOperationTask<Socket>(this);
            }
        }

        private class ReceiveOperation : AsyncOperation<int>
        {
            private SocketAsyncEventArgs _e;

            public ReceiveOperation()
            {
                _e = new SocketAsyncEventArgs();
                _e.Completed += OnCompleted;
            }

            private void OnCompleted(object sender, SocketAsyncEventArgs e)
            {
                Debug.Assert(e == _e);

                CompleteAsynchronously(_e.BytesTransferred);
            }

            public AsyncOperationTask<int> Run(Socket socket, ArraySegment<byte> buffer, SocketFlags flags)
            {
                Start();

                _e.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
                _e.SocketFlags = flags;
                bool pending = socket.ReceiveAsync(_e);

                if (!pending)
                {
                    CompleteSynchronously(_e.BytesTransferred);
                }

                return new AsyncOperationTask<int>(this);
            }
        }

        private class SendOperation : AsyncOperation<int>
        {
            private SocketAsyncEventArgs _e;

            public SendOperation()
            {
                _e = new SocketAsyncEventArgs();
                _e.Completed += OnCompleted;
            }

            private void OnCompleted(object sender, SocketAsyncEventArgs e)
            {
                Debug.Assert(e == _e);

                CompleteAsynchronously(_e.BytesTransferred);
            }

            public AsyncOperationTask<int> Run(Socket socket, ArraySegment<byte> buffer, SocketFlags flags)
            {
                Start();

                _e.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
                _e.SocketFlags = flags;
                bool pending = socket.SendAsync(_e);

                if (!pending)
                {
                    CompleteSynchronously(_e.BytesTransferred);
                }

                return new AsyncOperationTask<int>(this);
            }
        }

        // TODO: Do I actually need classes here, or can i get away with lambdas?

        public static Func<Socket, AsyncOperationTask<Socket>> MakeAcceptOperation()
        {
            var op = new AcceptOperation();
            return op.Run;
        }

        public static Func<Socket, ArraySegment<byte>, SocketFlags, AsyncOperationTask<int>> MakeReceiveOperation()
        {
            var op = new ReceiveOperation();
            return op.Run;
        }

        public static Func<Socket, ArraySegment<byte>, SocketFlags, AsyncOperationTask<int>> MakeSendOperation()
        {
            var op = new SendOperation();
            return op.Run;
        }
    }

    // TODO: Exception support
    // TODO: Async coordination here needs review

    public abstract class AsyncOperationBase<T>
    {
        private const int State_Reset = 0;              // Initial state, ready to be invoked
        private const int State_Started = 1;            // Async operation is started, no completion call back set yet
        private const int State_Pending = 2;            // Async operation is pending and _onComplete set.
                                                        // Async callback will invoke the _onComplete in CompleteAsynchronously
        private const int State_Complete = 3;           // Async operation is complete, but result has not yet been retrieved

        private int _state = State_Reset;
        private T _result = default(T);
        private Action _onCompleted;

        public bool IsCompleted
        {
            get
            {
                Debug.Assert(_state == State_Started || _state == State_Complete);

                return (_state == State_Complete);
            }
        }

        protected T ProtectedGetResult()
        {
            Debug.Assert(_state == State_Complete);
            Debug.Assert(_onCompleted == null);

            var result = _result;

            // Prepare for next invocation
            _result = default(T);
            _state = State_Reset;

            return _result;
        }

        public void OnCompleted(Action continuation)
        {
            Debug.Assert(_onCompleted == null);

            if (_state == State_Started)
            {
                // Try to atomically change our state from State_Started to State_Pending
                _onCompleted = continuation;
                var previousState = Interlocked.CompareExchange(ref _state, State_Pending, State_Started);
                if (previousState == State_Started)
                {
                    // Transition was successful.  Callback will be invoked in CompleteAsynchronously.
                    return;
                }

                // Transition failed.  CompleteAsynchronously was called before we could transition.
                // We must now be in State_Complete.
                _onCompleted = null;
            }

            Debug.Assert(_state == State_Complete);

            // Invoke _onCompleted here
            InvokeCompletion();
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            OnCompleted(continuation);
        }

        private void InvokeCompletion()
        {
            var onCompleted = _onCompleted;
            Debug.Assert(onCompleted != null);
            _onCompleted = null;

            onCompleted();
        }

        // For use by derived classes

        protected void Start()
        {
            Debug.Assert(_state == State_Reset);
            Debug.Assert(_onCompleted == null);

            _state = State_Started;
        }

        protected void CompleteSynchronously(T result)
        {
            Debug.Assert(_state == State_Started);
            Debug.Assert(_onCompleted == null);

            _result = result;
            _state = State_Complete;
        }

        protected void CompleteAsynchronously(T result)
        {
            _result = result;

            // Atomically change our state to State_Complete, and see what we were in before
            var previousState = Interlocked.Exchange(ref _state, State_Complete);

            if (previousState == State_Pending)
            {
                // The caller set a completion, so we need to invoke that here
                // Also, clear out _onCompleted to prepare for next invocation

                var onCompleted = _onCompleted;
                Debug.Assert(onCompleted != null);
                _onCompleted = null;

                onCompleted();
            }
            else
            {
                // No completion has been set yet -- we must be in State_Started.
                // Do nothing here (we already set _state to State_Completed).
                // If the caller ends up calling OnCompleted due to a race (i.e., they call IsComplete
                // and get false, but this routine executes before OnCallback is set), 
                // we'll invoke the completion there.

                Debug.Assert(previousState == State_Started);
            }
        }
    }

    // TODO: Need to fix this, it needs separate methods for CompletedSync/Async

    // TODO: Can I use something other than bool here?
    // TODO: Is there a way to simplify all these structs etc?

    public abstract class AsyncOperation : AsyncOperationBase<bool>
    {
        public void GetResult()
        {
            // (Currently does nothing, but should throw when exception support is added)
            ProtectedGetResult();
        }
    }

    public abstract class AsyncOperation<T> : AsyncOperationBase<T>
    {
        public T GetResult()
        {
            return ProtectedGetResult();
        }
    }

    public struct AsyncOperationTaskAwaiter : ICriticalNotifyCompletion
    {
        private readonly AsyncOperation _op;

        internal AsyncOperationTaskAwaiter(AsyncOperation op)
        {
            _op = op;
        }

        public bool IsCompleted
        {
            get { return _op.IsCompleted; }
        }

        public void GetResult()
        {
            _op.GetResult();
        }

        public void OnCompleted(Action continuation)
        {
            _op.OnCompleted(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            _op.UnsafeOnCompleted(continuation);
        }
    }

    public struct AsyncOperationTaskAwaiter<T> : ICriticalNotifyCompletion
    {
        private readonly AsyncOperation<T> _op;

        internal AsyncOperationTaskAwaiter(AsyncOperation<T> op)
        {
            _op = op;
        }

        public bool IsCompleted
        {
            get { return _op.IsCompleted; }
        }

        public T GetResult()
        {
            return _op.GetResult();
        }

        public void OnCompleted(Action continuation)
        {
            _op.OnCompleted(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            _op.UnsafeOnCompleted(continuation);
        }
    }

    public struct AsyncOperationTask
    {
        private readonly AsyncOperation _op;

        internal AsyncOperationTask(AsyncOperation op)
        {
            _op = op;
        }

        public AsyncOperationTaskAwaiter GetAwaiter()
        {
            return new AsyncOperationTaskAwaiter(_op);
        }
    }

    public struct AsyncOperationTask<T>
    {
        private readonly AsyncOperation<T> _op;

        internal AsyncOperationTask(AsyncOperation<T> op)
        {
            _op = op;
        }

        public AsyncOperationTaskAwaiter<T> GetAwaiter()
        {
            return new AsyncOperationTaskAwaiter<T>(_op);
        }
    }
}
