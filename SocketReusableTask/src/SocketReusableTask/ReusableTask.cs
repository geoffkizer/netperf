using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace SocketReusableTask
{
    // TODO: ExecutionContext support
    // TODO: Cancellation
    // TODO: Async coordination here needs review.
    // TODO: Some of these Asserts should possibly be exceptions.

    public abstract class ReusableTaskBase<T>
    {
        private const int State_Reset = 0;              // Initial state
        private const int State_Pending = 1;            // Async operation is pending and _onComplete set.
                                                        // Async callback will invoke the _onComplete in CompleteAsynchronously
        private const int State_Complete = 2;           // Async operation is complete, but result has not yet been retrieved

        private int _state = State_Reset;
        private T _result = default(T);
        private Exception _exception = null;
        private Action _onCompleted = null;

        internal bool IsCompleted => (_state == State_Complete);

        internal T GetResult()
        {
            Debug.Assert(_state == State_Complete);
            Debug.Assert(_onCompleted == null);

            var result = _result;
            var exception = _exception;

            // Prepare for next invocation
            _result = default(T);
            _exception = null;
            _state = State_Reset;

            if (exception != null)
            {
                // TODO, is this correct?  Probably not...
                throw exception;
            }

            return _result;
        }

        internal void OnCompleted(Action continuation)
        {
            // TODO: ExecutionContext handling
            UnsafeOnCompleted(continuation);
        }

        internal void UnsafeOnCompleted(Action continuation)
        {
            Debug.Assert(_onCompleted == null);

            _onCompleted = continuation;

            if (_state == State_Reset)
            {
                // The async operation hasn't completed yet
                // Try to atomically change our state from State_Reset to State_Pending
                var previousState = Interlocked.CompareExchange(ref _state, State_Pending, State_Reset);
                if (previousState == State_Reset)
                {
                    // Transition was successful.  Callback will be invoked in CompleteAsynchronously.
                    return;
                }

                // Transition failed.  CompleteAsynchronously was called before we could transition.
                // We must now be in State_Complete.
            }

            InvokeCompletion();
        }

        internal void SetResult(T result)
        {
            _result = result;
            Complete();
        }

        internal void SetException(Exception exception)
        {
            _exception = exception;
            Complete();
        }

        private void Complete()
        {
            // Atomically change our state to State_Complete, and see what we were in before
            var previousState = Interlocked.Exchange(ref _state, State_Complete);

            if (previousState == State_Pending)
            {
                // The caller set a completion, so we need to invoke that here
                InvokeCompletion();
            }
            else
            {
                Debug.Assert(previousState == State_Reset);
            }
        }

        private void InvokeCompletion()
        {
            Debug.Assert(_state == State_Complete);
            Debug.Assert(_onCompleted != null);

            var onCompleted = _onCompleted;
            _onCompleted = null;

            onCompleted();
        }
    }

    // TODO: Can I use something other than bool here?

    public sealed class ReusableTask : ReusableTaskBase<bool>
    {
        internal ReusableTask()
        {
        }

        public ReusableTaskAwaiter GetAwaiter()
        {
            return new ReusableTaskAwaiter(this);
        }
    }

    public sealed class ReusableTask<T> : ReusableTaskBase<T>
    {
        internal ReusableTask()
        {
        }

        public ReusableTaskAwaiter<T> GetAwaiter()
        {
            return new ReusableTaskAwaiter<T>(this);
        }
    }

    public struct ReusableTaskAwaiter : ICriticalNotifyCompletion
    {
        private readonly ReusableTask _op;

        internal ReusableTaskAwaiter(ReusableTask op)
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

    public struct ReusableTaskAwaiter<T> : ICriticalNotifyCompletion
    {
        private readonly ReusableTask<T> _op;

        internal ReusableTaskAwaiter(ReusableTask<T> op)
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

    // CONSIDER: May want a "Reset" call here, so that users of this class
    // can be explicit about when they are going to use this again, allowing us better integrity checks.
    // CONSIDER: Should this be a struct?

    public sealed class ReusableTaskCompletionSource
    {
        private readonly ReusableTask _task = null;

        public ReusableTaskCompletionSource()
        {
            _task = new ReusableTask();
        }

        public void SetResult()
        {
            _task.SetResult(true);
        }

        public void SetException(Exception e)
        {
            _task.SetException(e);
        }

        public ReusableTask Task => _task;
    }

    public sealed class ReusableTaskCompletionSource<T>
    {
        private ReusableTask<T> _task = null;

        public ReusableTaskCompletionSource()
        {
            _task = new ReusableTask<T>();
        }

        public void SetResult(T result)
        {
            _task.SetResult(result);
        }

        public void SetException(Exception e)
        {
            _task.SetException(e);
        }

        public ReusableTask<T> Task => _task;
    }
}
