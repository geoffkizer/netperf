using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SocketAPM
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
            private AsyncCallback _acceptCallback;
            private AsyncCallback _readCallback;
            private AsyncCallback _writeCallback;

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
                s_listenSocket.BeginAccept(_acceptCallback, null);
            }

            private void OnAccept(IAsyncResult ar)
            {
                _socket = s_listenSocket.EndAccept(ar);

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
                try
                {
                    _socket.BeginReceive(_readBuffer, 0, _readBuffer.Length, SocketFlags.None, _readCallback, null);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        _socket.Dispose();
                        return;
                    }

                    // Not clear why ConnectionAborted happens here.  Is this a bug?
                    if (e.SocketErrorCode == SocketError.ConnectionAborted)
                    {
                        _socket.Dispose();
                        return;
                    }

                    Console.WriteLine("Read failed synchronously, SocketError = {0}", e.SocketErrorCode);
                    throw;
                }
            }

            private void OnRead(IAsyncResult ar)
            {
                if (s_trace)
                {
                    if (ar.CompletedSynchronously)
                    {
                        Console.WriteLine("Read completed synchronously");
                    }
                }

                int bytesRead;
                try
                {
                    bytesRead = _socket.EndReceive(ar);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.ConnectionReset)
                    {
                        _socket.Dispose();
                        return;
                    }

                    Console.WriteLine("Read failed, SocketError = {0}", e.SocketErrorCode);
                    throw;
                }

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

                _socket.BeginSend(s_responseMessage, 0, s_responseMessage.Length, SocketFlags.None, _writeCallback, null);
            }

            private void OnWrite(IAsyncResult ar)
            {
                if (s_trace)
                {
                    if (ar.CompletedSynchronously)
                    {
                        Console.WriteLine("Write completed synchronously");
                    }
                }

                int bytesWritten = _socket.EndSend(ar);

                if (s_trace)
                {
                    Console.WriteLine("Write complete, bytesWritten = {0}", bytesWritten);
                }

                if (bytesWritten != s_responseMessage.Length)
                {
                    throw new Exception(string.Format("unexpected write size, bytesWritten = {0}", bytesWritten));
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

