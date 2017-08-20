using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace SslStreamPerf
{
    internal static class LoopbackTest
    {
        // Avoid compiler warning
        private static void SpawnTask(Func<Task> a)
        {
            Task.Run(a);
        }

        private static async Task<ClientHandler> StartOneAsync(EndPoint serverEndpoint, int messageSize)
        {
            Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(serverEndpoint);

            Stream s = new NetworkStream(client);
            s = await SslHelper.GetClientStream(s);

            var clientHandler = new ClientHandler(s, messageSize);
            return clientHandler;
        }

        private static async Task RunServer(Socket listen, X509Certificate2 cert, int messageSize)
        {
            while (true)
            {
                Socket accept = await listen.AcceptAsync();
                SpawnTask(async () =>
                {
                    Stream s = new NetworkStream(accept);
                    s = await SslHelper.GetServerStream(s, cert);

                    var serverHandler = new ServerHandler(s, messageSize);
                    await serverHandler.Run();
                });
            }
        }

        public static ClientHandler[] Start(X509Certificate2 cert, int clientCount, int messageSize)
        {
            Socket listen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listen.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listen.Listen(clientCount);
            var listenAddress = listen.LocalEndPoint;

            SpawnTask(() => RunServer(listen, cert, messageSize));

            var tasks = Enumerable.Range(0, clientCount)
                            .Select(_ => StartOneAsync(listenAddress, messageSize))
                            .ToArray();

            Task.WaitAll(tasks);

            var handlers = tasks.Select(t => t.Result).ToArray();
            foreach (var h in handlers)
            {
                SpawnTask(() => h.Run());
            }

            return handlers;
        }
    }
}
