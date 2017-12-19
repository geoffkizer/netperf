using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace SocketPerfTest
{
    public sealed class BoundIOThreads
    {
        private readonly IntPtr[] _completionPortHandles;
        private int _nextCompletionPort;

        public BoundIOThreads(int batchSize)
        {
            // Create completion ports and threads
            // We create 1 per core; we assume that callbacks generally do not block
            // and so this is will saturate CPU.
            _completionPortHandles = new IntPtr[Environment.ProcessorCount];
            for (int i = 0; i < _completionPortHandles.Length; i++)
            {
                _completionPortHandles[i] = Interop.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, IntPtr.Zero, 0);
                if (_completionPortHandles[i] == IntPtr.Zero)
                    throw new Exception("Completion port creation failed");

                // Create one thread for this completion port
                IOThread.Spawn(_completionPortHandles[i], batchSize);
            }

            _nextCompletionPort = 0;
        }

        public void Bind(Socket s)
        {
            // Round robin across the completion ports
            int completionPort = Volatile.Read(ref _nextCompletionPort);
            while (true)
            {
                Debug.Assert(completionPort < _completionPortHandles.Length);
                int next = completionPort + 1;
                if (next == _completionPortHandles.Length)
                    next = 0;

                int seen = Interlocked.CompareExchange(ref _nextCompletionPort, next, completionPort);
                if (seen == completionPort)
                    break;

                completionPort = seen;
            }

            IOThread.Bind(s, _completionPortHandles[completionPort]);
        }
    }
}
