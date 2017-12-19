using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace SocketPerfTest
{
    public static class IOThread
    {
        public static Thread Spawn(IntPtr completionPortHandle, int batchSize)
        {
            Thread t;
            if (batchSize == 0)
            {
                t = new Thread(() => ThreadProc(completionPortHandle));
            }
            else
            {
                t = new Thread(() => BatchedThreadProc(completionPortHandle, batchSize));
            }

            t.Start();
            return t;
        }

        public static unsafe void ThreadProc(IntPtr completionPortHandle)
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

        public static unsafe void BatchedThreadProc(IntPtr completionPortHandle, int batchSize)
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

        public static void Bind(Socket s, IntPtr completionPortHandle)
        {
            // Bind socket to completion port
            var result = Interop.CreateIoCompletionPort(s.Handle, completionPortHandle, IntPtr.Zero, 0);
            if (result != completionPortHandle)
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
