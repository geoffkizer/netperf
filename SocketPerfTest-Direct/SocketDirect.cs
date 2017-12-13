using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Net.Sockets;
using System.Diagnostics;

namespace SocketPerfTest
{
    public static class SocketDirect
    {
        // Interop definitions

        [StructLayout(LayoutKind.Sequential)]
        private struct WSABuffer
        {
            internal int Length; // Length of Buffer
            internal IntPtr Pointer;// Pointer to Buffer
        }

        [DllImport("ws2_32", SetLastError = true)]
        private static unsafe extern int WSARecv(
            IntPtr socketHandle,
            WSABuffer* buffers,
            int bufferCount,
            out int bytesTransferred,
            ref SocketFlags socketFlags,
            NativeOverlapped* overlapped,
            IntPtr completionRoutine);

        [DllImport("ws2_32", SetLastError = true)]
        private static extern unsafe int WSASend(
            IntPtr socketHandle,
            WSABuffer* buffers,
            int bufferCount,
            out int bytesTransferred,
            SocketFlags socketFlags,
            NativeOverlapped* overlapped,
            IntPtr completionRoutine);

        private static SocketError GetLastSocketError()
        {
            int win32Error = Marshal.GetLastWin32Error();
            Debug.Assert(win32Error != 0, "Expected non-0 error");
            return (SocketError)win32Error;
        }

        // Return true on success, false on pending; throw on error
        // Note, only single buffer supported for now
        public static unsafe bool Receive(
            IntPtr socketHandle,
            byte* buffer,
            int bufferLength,
            out int bytesTransferred,
            ref SocketFlags socketFlags,
            NativeOverlapped* nativeOverlapped)
        {
            WSABuffer wsaBuffer;
            wsaBuffer.Pointer = (IntPtr)buffer;
            wsaBuffer.Length = bufferLength;

            if (WSARecv(socketHandle, &wsaBuffer, 1, out bytesTransferred, ref socketFlags, nativeOverlapped, IntPtr.Zero) == 0)
            {
                return true;
            }

            SocketError error = GetLastSocketError();
            if (error != SocketError.IOPending)
            {
                throw new SocketException((int)error);
            }

            return false;
        }

        // Return true on success, false on pending; throw on error
        // Note, only single buffer supported for now
        public static unsafe bool Send(
            IntPtr socketHandle,
            byte* buffer,
            int bufferLength,
            out int bytesTransferred,
            SocketFlags socketFlags,
            NativeOverlapped* nativeOverlapped)
        {
            WSABuffer wsaBuffer;
            wsaBuffer.Pointer = (IntPtr)buffer;
            wsaBuffer.Length = bufferLength;

            if (WSASend(socketHandle, &wsaBuffer, 1, out bytesTransferred, socketFlags, nativeOverlapped, IntPtr.Zero) == 0)
            {
                return true;
            }

            SocketError error = GetLastSocketError();
            if (error != SocketError.IOPending)
            {
                throw new SocketException((int)error);
            }

            return false;
        }

    }

}
