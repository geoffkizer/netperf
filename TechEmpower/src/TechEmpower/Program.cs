using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace TechEmpower
{
    public class Program
    {
        const int BufferSize = 4096;

        const string Response = "HTTP/1.1 200 OK\r\nServer: TechEmpowerTest\r\nContent-Type: text/plain\r\nContent-Length: 13\r\n\r\nhello world\r\n";

        const string PipelinedResponse =
            Response + Response + Response + Response + 
            Response + Response + Response + Response + 
            Response + Response + Response + Response +
            Response + Response + Response + Response;

        static IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 5000);
        static TcpListener listener;

        static byte[] ResponseBuffer = System.Text.Encoding.UTF8.GetBytes(Response);

        static byte[] PipelinedResponseBuffer = System.Text.Encoding.UTF8.GetBytes(PipelinedResponse);

        public static async void ProcessConnection(TcpClient client)
        {
//            Console.WriteLine("Connection accepted");

            var buffer = new byte[BufferSize];

            using (client)
            {
                using (var s = client.GetStream())
                {
                    while (true)
                    {
                        int bytesRead = await s.ReadAsync(buffer, 0, BufferSize);

//                        Console.WriteLine("Bytes read = {0}", bytesRead);

                        if (bytesRead == 0)
                        {
//                            Console.WriteLine("Disconnected");
                            break;
                        }

                        await s.WriteAsync(PipelinedResponseBuffer, 0, PipelinedResponseBuffer.Length);
                    }
                }
            }
        }

        public static async void Accept()
        {
            var client = await listener.AcceptTcpClientAsync();

            // Spawn another accept to handle the next connection

            Task.Run(() => Accept());

            ProcessConnection(client);
        }

        public static void RunServer()
        {
            listener = new TcpListener(endPoint);

            listener.Start();

            Task.Run(() => Accept());

            Console.WriteLine("Server running on {0}", endPoint);

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        public static void Main(string[] args)
        {
            RunServer();
        }
    }
}
