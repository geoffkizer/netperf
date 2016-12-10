using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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

        public const int s_expectedReadSize = 848;

        class Connection
        {
            private SocketAsyncEventArgs _acceptEventArgs;
            private SocketAsyncEventArgs _readEventArgs;
            private SocketAsyncEventArgs _writeEventArgs;

            private Socket _socket;
            // buffer?

            public Connection()
            {
                _acceptEventArgs = new SocketAsyncEventArgs();
                _acceptEventArgs.Completed += OnAccept;

                _readEventArgs = new SocketAsyncEventArgs();
                _readEventArgs.SetBuffer(new byte[4096], 0, 4096);
                _readEventArgs.Completed += OnRead;

                _writeEventArgs = new SocketAsyncEventArgs();
                _writeEventArgs.SetBuffer(s_responseMessage, 0, s_responseMessage.Length);
                _writeEventArgs.Completed += OnWrite;
            }

            public void DoAccept()
            {
                bool pending = s_listenSocket.AcceptAsync(_acceptEventArgs);
                if (!pending)
                    OnAccept(null, _acceptEventArgs);
            }

            private void OnAccept(object sender, SocketAsyncEventArgs e)
            {
                if (e.SocketError != SocketError.Success)
                {
                    throw new Exception("accept failed");
                }

                if (s_trace)
                {
                    Console.WriteLine("Connection accepted");
                }

                // Spawn another work item to handle next connection
                QueueConnectionHandler();

                _socket = e.AcceptSocket;
                _socket.NoDelay = true;

                DoRead();
            }

            private void DoRead()
            {
                bool pending = _socket.ReceiveAsync(_readEventArgs);
                if (!pending)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Read completed synchronously");
                    }

                    OnRead(null, _readEventArgs);
                }
            }

            private void OnRead(object sender, SocketAsyncEventArgs e)
            {
                if (e.SocketError != SocketError.Success)
                {
                    if (e.SocketError == SocketError.ConnectionReset)
                    {
                        _socket.Dispose();
                        return;
                    }

                    throw new Exception(string.Format("read failed, error = {0}", e.SocketError));
                }

                int bytesRead = e.BytesTransferred;

                if (bytesRead == 0)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Connection closed by client");
                    }

                    _socket.Dispose();
                    return;
                }

                if (s_trace)
                {
                    Console.WriteLine("Read complete, bytesRead = {0}", bytesRead);
                }

                if (bytesRead != s_expectedReadSize)
                {
                    throw new Exception(string.Format("unexpected read size, bytesRead = {0}", bytesRead));
                }

                // Do write now

                bool pending = _socket.SendAsync(_writeEventArgs);
                if (!pending)
                {
                    if (s_trace)
                    {
                        Console.WriteLine("Write completed synchronously");
                    }

                    OnWrite(null, _writeEventArgs);
                }
            }

            private void OnWrite(object sender, SocketAsyncEventArgs e)
            {
                if (e.SocketError != SocketError.Success)
                {
                    throw new Exception("write failed");
                }

                int bytesWritten = e.BytesTransferred;

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
