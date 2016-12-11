using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketTAP
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

        public const int s_expectedReadSize = 848;

        class Connection
        {
            private Action<Task<Socket>> _acceptCallback;
            private Action<Task<int>> _readCallback;
            private Action<Task<int>> _writeCallback;

            private Socket _socket;
            private byte[] _readBuffer = new byte[4096];

            public Connection()
            {
                _acceptCallback = this.OnAccept;
                _readCallback = this.OnRead;
                _writeCallback = this.OnWrite;
            }

            public void DoAccept()
            {
                s_listenSocket.AcceptAsync().ContinueWith(_acceptCallback);
            }

            private void OnAccept(Task<Socket> t)
            {
                _socket = t.Result;

                if (s_trace)
                {
                    Console.WriteLine("Connection accepted");
                }

                // Spawn another work item to handle next connection
                QueueConnectionHandler();

                _socket.NoDelay = true;

                DoRead();
            }

            private void DoRead()
            {
                _socket.ReceiveAsync(new ArraySegment<byte>(_readBuffer), SocketFlags.None).ContinueWith(_readCallback);
            }

            private void OnRead(Task<int> t)
            {
                int bytesRead;
                try
                {
                    bytesRead = t.Result;
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        _socket.Dispose();
                        return;
                    }

#if false
                    // Not clear why ConnectionAborted happens here.  Is this a bug?
                    if (e.SocketErrorCode == SocketError.ConnectionAborted)
                    {
                        _socket.Dispose();
                        return;
                    }
#endif

                    Console.WriteLine("Read failed, SocketError = {0}", e.SocketErrorCode);
                    throw;
                }

                if (bytesRead == 0)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Connection closed by client");
                    }

                    return;
                }

                if (s_trace)
                {
                    Console.WriteLine("Read complete, bytesRead = {0}", bytesRead);
                }

                // Do write now

                _socket.SendAsync(new ArraySegment<byte>(s_responseMessage), SocketFlags.None).ContinueWith(_writeCallback);
            }

            private void OnWrite(Task<int> t)
            {
                int bytesWritten = t.Result;

                if (s_trace)
                {
                    Console.WriteLine("Write complete, bytesWritten = {0}", bytesWritten);
                }

                DoRead();
            }
        }

        private static void HandleConnection(object state)
        {
            var c = new Connection();
            c.DoAccept();
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

