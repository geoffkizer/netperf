using System;
using System.Net.Sockets;

namespace SocketPerfTest
{
    public sealed class CustomThreadPool
    {
        private readonly IntPtr _completionPortHandle;

        public CustomThreadPool(int batchSize)
        {
            // Create completion port
            _completionPortHandle = Interop.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, IntPtr.Zero, 0);
            if (_completionPortHandle == IntPtr.Zero)
                throw new Exception("Completion port creation failed");

            // Create thread pool threads
            // We create 2 per core; we assume that callbacks generally do not block
            // and so this is more than enough to saturate CPU.
            for (int i = 0; i < Environment.ProcessorCount * 2; i++)
            {
                IOThread.Spawn(_completionPortHandle, batchSize);
            }
        }

        public void Bind(Socket s)
        {
            IOThread.Bind(s, _completionPortHandle);
        }
    }
}
