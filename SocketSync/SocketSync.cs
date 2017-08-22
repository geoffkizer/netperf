﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketAwait
{
    public class Program
    {
        public static Socket s_listenSocket;

        public const bool s_trace = false;

        public static readonly byte[] s_responseMessage = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n");

        public const int s_expectedReadSize = 2624;

        class Connection
        {
            private Socket _socket;
            private byte[] _readBuffer = new byte[4096];

            public Connection()
            {
            }

            public void Run()
            {
                _socket = s_listenSocket.Accept();

                if (s_trace)
                {
                    Console.WriteLine("Connection accepted");
                }

                // Spawn another work item to handle next connection
                QueueConnectionHandler();

                _socket.NoDelay = true;

                while (true)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = _socket.Receive(_readBuffer, 0, _readBuffer.Length, SocketFlags.None);
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

#if false
                    if (bytesRead != s_expectedReadSize)
                    {
                        throw new Exception(string.Format("unexpected read size, bytesRead = {0}", bytesRead));
                    }
#endif
                    for (var i = 0; i < 16; i++)
                    {
                        int bytesWritten = _socket.Send(s_responseMessage, 0, s_responseMessage.Length, SocketFlags.None);
                        if (s_trace)
                        {
                            Console.WriteLine("Write complete, bytesWritten = {0}", bytesWritten);
                        }
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

