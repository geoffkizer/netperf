using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace SslStreamPerf
{
    internal static class ClientRunner
    {
        private static async Task<ClientHandler> StartOneAsync(IPEndPoint serverEndpoint, bool useSsl, int messageSize)
        {
            Socket client = new Socket(serverEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(serverEndpoint);

            Stream s = new NetworkStream(client);
            if (useSsl)
            {
                s = await SslHelper.GetClientStream(s);
            }

            var clientHandler = new ClientHandler(s, messageSize);
            return clientHandler;
        }

        public static ClientHandler[] Run(IPEndPoint serverEndpoint, bool useSsl, int clientCount, int messageSize)
        {
            var tasks = Enumerable.Range(0, clientCount)
                            .Select(_ => StartOneAsync(serverEndpoint, useSsl, messageSize))
                            .ToArray();

            Task.WaitAll(tasks);

            var handlers = tasks.Select(t => t.Result).ToArray();
            foreach (var h in handlers)
            {
                TaskHelper.SpawnTask(() => h.Run());
            }

            return handlers;
        }
    }
}
