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
        private static async Task<ConnectionHandler> StartOneAsync(IPEndPoint serverEndpoint, bool useSsl, ConnectionManager connectionManager)
        {
            Socket client = new Socket(serverEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(serverEndpoint);

            client.NoDelay = true;
            Stream s = new NetworkStream(client);
            if (useSsl)
            {
                s = await SslHelper.GetClientStream(s);
            }

            var clientHandler = new ConnectionHandler(connectionManager, s, isClient: true);
            return clientHandler;
        }

        public static ConnectionHandler[] Run(IPEndPoint serverEndpoint, bool useSsl, int clientCount, int messageSize)
        {
            ConnectionManager connectionManager = new ConnectionManager(messageSize);

            var tasks = Enumerable.Range(0, clientCount)
                            .Select(_ => StartOneAsync(serverEndpoint, useSsl, connectionManager))
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
