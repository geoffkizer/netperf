using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;

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
                if (batchSize == 0)
                {
                    new Thread(() => ThreadProc(_completionPortHandle)).Start();
                }
                else
                {
                    new Thread(() => BatchedThreadProc(_completionPortHandle, batchSize)).Start();
                }
            }
        }

        private static unsafe void ThreadProc(IntPtr completionPortHandle)
        {
            while (true)
            {
                int bytesTransferred;
                NativeOverlapped* nativeOverlapped;
                bool success = Interop.GetQueuedCompletionStatus(completionPortHandle, out bytesTransferred, out _, out nativeOverlapped, -1);

                if (nativeOverlapped == null)
                    throw new Exception("GetQueuedCompletionStatus failed???");

                int error = success ? 0 : Marshal.GetLastWin32Error();

                SimpleOverlapped.IOCompletionCallback(error, bytesTransferred, nativeOverlapped);
            }
        }

        private static unsafe void BatchedThreadProc(IntPtr completionPortHandle, int batchSize)
        {
            Interop.OverlappedEntry* entries = stackalloc Interop.OverlappedEntry[batchSize];

            while (true)
            {
                bool success = Interop.GetQueuedCompletionStatusEx(completionPortHandle, entries, (uint)batchSize, out uint count, -1, false);

                if (!success)
                {
                    throw new Exception("GetQueuedCompletionStatusEx failed");
                }

                if (count == 0)
                {
                    throw new Exception("GetQueuedCompletionStatusEx returned 0 entries");
                }

                for (uint i = 0; i < count; i++)
                {
                    int errorCode = (int)entries[i].lpOverlapped->InternalLow.ToInt64();
                    int bytesTransferred = entries[i].dwNumberOfBytesTransferred;
                    NativeOverlapped* nativeOverlapped = entries[i].lpOverlapped;
                    SimpleOverlapped.IOCompletionCallback(errorCode, bytesTransferred, nativeOverlapped);
                }
            }
        }

        public void Bind(Socket s)
        {
            // Bind socket to completion port
            var result = Interop.CreateIoCompletionPort(s.Handle, _completionPortHandle, IntPtr.Zero, 0);
            if (result != _completionPortHandle)
                throw new Exception("Completion port bind failed");

            // Set completion mode
            if (!Interop.SetFileCompletionNotificationModes(s.Handle,
                Interop.FileCompletionNotificationModes.SkipCompletionPortOnSuccess | Interop.FileCompletionNotificationModes.SkipSetEventOnHandle))
            {
                throw new Exception("SetFileCompletionNotificationModes failed");
            }
        }
    }
}
