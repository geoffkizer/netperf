using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using System.Buffers;
using System.IO.Pipelines.Networking.Libuv;

namespace SocketEvents
{
    public class Program
    {
        public static UvTcpListener s_listener;

        public const bool s_trace = true;

        public static readonly Buffer<byte> s_responseMessage = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nServer: TestServer\r\nDate: Sun, 06 Nov 1994 08:49:37 GMT\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n");

        public const int s_expectedReadSize = 2624;

        private static void Start()
        {
            var thread = new UvThread();
            var listener = new UvTcpListener(thread, new IPEndPoint(IPAddress.Any, 5000));
            listener.OnConnection(async connection =>
            {
                while (true)
                {
                    // Wait for data
                    var result = await connection.Input.ReadAsync();
                    var input = result.Buffer;
                    var consumed = input.Start;

                    if (input.IsEmpty && result.IsCompleted)
                    {
                        // No more data
                        if (s_trace)
                        {
                            Console.WriteLine("Connection closed by client");
                        }
                        
                        break;
                    }

                    if (s_trace)
                    {
                        Console.WriteLine($"Received {input.Length} bytes");
                    }

                    // Send response
                    for (var i = 0; i < 16; i++)
                    {
                        var output = connection.Output.Alloc(s_responseMessage.Length);
                        s_responseMessage.CopyTo(output.Buffer);
                        output.Advance(s_responseMessage.Length);
                        await output.FlushAsync();
                    }

                    // Consume the input
                    consumed = input.Move(consumed, input.Length);
                    connection.Input.Advance(consumed, consumed);
                }
            });

            listener.StartAsync().GetAwaiter().GetResult();
        }

        public static void Main(string[] args)
        {
            Start();

            Console.WriteLine("Server Running");
            Console.ReadLine();
        }
    }
}
