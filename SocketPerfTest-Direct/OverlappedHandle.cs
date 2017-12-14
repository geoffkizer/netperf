using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Net.Sockets;

namespace SocketPerfTest
{
    // CONSIDER: This could just be a struct.

    public sealed unsafe class SimpleOverlapped : IDisposable
    {
        private NativeOverlappedWithCallback* _nativeOverlappedWithCallback;

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct NativeOverlappedWithCallback
        {
            // NativeOverlapped must be the first member, since we cast to/from it
            public NativeOverlapped _nativeOverlapped;
            public IntPtr _completionCallbackHandle;
        }

        public SimpleOverlapped(Action<int, int> completionCallback)
        {
            if (completionCallback == null)
                throw new ArgumentNullException(nameof(completionCallback));

            _nativeOverlappedWithCallback = (NativeOverlappedWithCallback*)Marshal.AllocHGlobal(sizeof(NativeOverlappedWithCallback));
            _nativeOverlappedWithCallback->_nativeOverlapped = default(NativeOverlapped);
            _nativeOverlappedWithCallback->_completionCallbackHandle = GCHandle.ToIntPtr(GCHandle.Alloc(completionCallback));
        }

        public void Dispose()
        {
            if (_nativeOverlappedWithCallback != null)
            { 
                GCHandle.FromIntPtr(_nativeOverlappedWithCallback->_completionCallbackHandle).Free();
                Marshal.FreeHGlobal((IntPtr)_nativeOverlappedWithCallback);
                _nativeOverlappedWithCallback = null;
            }
        }

        public NativeOverlapped* GetNativeOverlapped()
        {
            return (NativeOverlapped*)_nativeOverlappedWithCallback;
        }

        public static unsafe void IOCompletionCallback(int errorCode, int bytesTransferred, NativeOverlapped* nativeOverlapped)
        {
            NativeOverlappedWithCallback* nativeOverlappedWithCallback = (NativeOverlappedWithCallback*)nativeOverlapped;

            Action<int, int> completionCallback = (Action<int, int>)GCHandle.FromIntPtr(nativeOverlappedWithCallback->_completionCallbackHandle).Target;

            completionCallback((int)errorCode, (int)bytesTransferred);
        }

#if false
        public static Action<int, int> GetSocketCallback(Action<SocketError, int> callback)
        {
            return (int errorCode, int bytesTransferred) =>
            {
                if (errorCode == 0)
                {
                    callback(SocketError.Success, (int)bytesTransferred);
                }
                else
                {
                    NativeOverlapped.
                    // Retrieve actual error code
                    if (Interop.WSAGetOverlappedResult(boundHandle.Handle.DangerousGetHandle(), nativeOverlapped, out _, false, out _))
                    {
                        Console.WriteLine($"Original error was {errorCode}, but WSAGetOverlappedResult returned success??? bytes={bytesTransferred}");

                        callback(SocketError.Success, (int)bytesTransferred);
                    }
                    else
                    {
                        SocketError socketError = SocketDirect.GetLastSocketError();
                        Debug.Assert(socketError != SocketError.IOPending);
                        Debug.Assert(socketError != SocketError.Success);

                        callback(socketError, 0);
                    }
                }
            }
        }
#endif
    }
}
