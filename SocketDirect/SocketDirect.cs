using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

namespace SocketEvents
{
    public class Program
    {
        public static Socket s_listenSocket;

        public const bool s_trace = false;

        public static readonly byte[] s_responseMessage = Encoding.UTF8.GetBytes(
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n" +
            "HTTP/1.1 200 OK\r\nServer: TestServer\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n");

        public static readonly GCHandle s_responseMessageGCHandle = GCHandle.Alloc(s_responseMessage, GCHandleType.Pinned);
        public static readonly unsafe byte* s_responseMessagePtr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(s_responseMessage, 0);

        public const int s_expectedReadSize = 848;

        private static SocketAsyncEventArgs s_acceptEventArgs;

        class Connection
        {
            private IntPtr _socketHandle;
            private byte[] _readBuffer;

            private GCHandle _readBufferGCHandle;

            private SocketDirect.OverlappedHandle _receiveOverlapped;
            private SocketDirect.OverlappedHandle _sendOverlapped;

            public Connection(Socket socket)
            {
                _socketHandle = socket.Handle;

                // Alloc and pin read buffer
                _readBuffer = new byte[4096];
                _readBufferGCHandle = GCHandle.Alloc(_readBuffer, GCHandleType.Pinned);

                // Allocate overlapped structures
                _receiveOverlapped = new SocketDirect.OverlappedHandle(OnRead);
                _sendOverlapped = new SocketDirect.OverlappedHandle(OnWrite);
            }

            public unsafe void Run()
            {
                SocketDirect.BindToWin32ThreadPool(_socketHandle);
                SocketDirect.SetNoDelay(_socketHandle);

                DoRead();
            }

            private unsafe void DoRead()
            {
                int bytesTransferred;
                SocketFlags socketFlags = SocketFlags.None;
                SocketError socketError = SocketDirect.Receive(
                    _socketHandle,
                    (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(_readBuffer, 0),
                    _readBuffer.Length,
                    out bytesTransferred,
                    ref socketFlags,
                    _receiveOverlapped);

                if (socketError != SocketError.IOPending)
                {
                    OnRead((int)socketError, bytesTransferred);
                }
            }

            private unsafe void OnRead(int errorCode, int bytesRead)
            {
                SocketError socketError = (SocketError)errorCode;

                if (socketError != SocketError.Success)
                {
                    if (socketError == SocketError.ConnectionReset)
                    {
                        // TODO: Dispose socket
                        return;
                    }

//                    throw new Exception(string.Format("read failed, error = {0}", socketError));
                    return;
                }

                if (bytesRead == 0)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Connection closed by client");
                    }
                    
                    // TODO
//                    _socket.Dispose();
                    return;
                }

                if (s_trace)
                {
                    Console.WriteLine("Read complete, bytesRead = {0}", bytesRead);
                }

#if false
                if (bytesRead != s_expectedReadSize)
                {
                    throw new Exception(string.Format("unexpected read size, bytesRead = {0}", bytesRead));
                }
#endif

                // Do write now

                int bytesTransferred;
                SocketFlags socketFlags = SocketFlags.None;
                socketError = SocketDirect.Send(
                    _socketHandle,
                    s_responseMessagePtr,
                    s_responseMessage.Length,
                    out bytesTransferred,
                    socketFlags,
                    _sendOverlapped);
                if (socketError == SocketError.Success)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Write completed synchronously");
                    }

                    OnWrite((int)socketError, bytesTransferred);
                }
            }

            private unsafe void OnWrite(int errorCode, int bytesWritten)
            {
                SocketError socketError = (SocketError)errorCode;

                if (socketError != SocketError.Success)
                {
                    throw new Exception("write failed");
                }

                if (bytesWritten != s_responseMessage.Length)
                {
                    throw new Exception(string.Format("unexpected write size, bytesWritten = {0}", bytesWritten));
                }

                if (s_trace)
                {
                    Console.WriteLine("Write complete, bytesWritten = {0}", bytesWritten);
                }

                DoRead();
            }
        }

        private static void HandleConnection(object state)
        {
            bool pending = s_listenSocket.AcceptAsync(s_acceptEventArgs);
            if (!pending)
                OnAccept(null, s_acceptEventArgs);
        }

        private static void OnAccept(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                throw new Exception("accept failed");
            }

            if (s_trace)
            {
                Console.WriteLine("Connection accepted");
            }

            Socket s = e.AcceptSocket;

            // Clear for next accept
            e.AcceptSocket = null;

            // Spawn another work item to handle next connection
            QueueConnectionHandler();

            var c = new Connection(s);
            c.Run();
        }

        private static void QueueConnectionHandler()
        {
            ThreadPool.QueueUserWorkItem(HandleConnection);
        }

        private static void Start()
        {
            s_listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            s_listenSocket.Bind(new IPEndPoint(IPAddress.Any, 5000));
            s_listenSocket.Listen(1000);

            s_acceptEventArgs = new SocketAsyncEventArgs();
            s_acceptEventArgs.Completed += OnAccept;

            QueueConnectionHandler();
        }

        public static void Main(string[] args)
        {
            Start();

            Console.WriteLine("Server Running");
            Console.ReadLine();
        }
    }
}
