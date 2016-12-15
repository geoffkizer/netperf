using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SocketEventTask
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

            public Connection()
            {
                _acceptEventArgs = new SocketAsyncEventArgs();

                _readEventArgs = new SocketAsyncEventArgs();
                _readEventArgs.SetBuffer(new byte[4096], 0, 4096);

                _writeEventArgs = new SocketAsyncEventArgs();
                _writeEventArgs.SetBuffer(s_responseMessage, 0, s_responseMessage.Length);
            }

            public async void Run()
            {
                await s_listenSocket.AcceptAsync2(_acceptEventArgs);
                _socket = _acceptEventArgs.AcceptSocket;

                if (s_trace)
                {
                    Console.WriteLine("Connection accepted");
                }

                // Spawn another work item to handle next connection
                QueueConnectionHandler();

                _socket.NoDelay = true;

                while (true)
                {
                    try
                    {
                        await _socket.ReceiveAsync2(_readEventArgs);
                    }
                    catch (SocketException e)
                    {
                        if (e.SocketErrorCode == SocketError.ConnectionReset)
                        {
                            _socket.Dispose();
                            return;
                        }

                        throw;
                    }

                    int bytesRead = _readEventArgs.BytesTransferred;

                    if (bytesRead == 0)
                    {
                        if (s_trace)
                        {
                            Console.WriteLine("Connection closed by client");
                        }

                        _socket.Dispose();
                        break;
                    }

                    if (s_trace)
                    {
                        Console.WriteLine("Read complete, bytesRead = {0}", bytesRead);
                    }

                    if (bytesRead != s_expectedReadSize)
                    {
                        throw new Exception(string.Format("unexpected read size, bytesRead = {0}", bytesRead));
                    }

                    await _socket.SendAsync2(_writeEventArgs);

                    int bytesWritten = _writeEventArgs.BytesTransferred;

                    if (s_trace)
                    {
                        Console.WriteLine("Write complete, bytesWritten = {0}", bytesWritten);
                    }
                }
            }
        }

        private static void HandleConnection(object state)
        {
            var c = new Connection();
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
