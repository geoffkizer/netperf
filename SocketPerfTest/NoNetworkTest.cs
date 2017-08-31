using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.IO;

namespace SslStreamPerf
{
    internal static class NoNetworkTest
    {
        private static async Task<(ClientHandler clientHandler, ServerHandler serverHandler)> StartOneAsync(X509Certificate2 cert, int messageSize)
        {
            // TODO: Use updated ProducerConsumerStream
            (Stream s1, Stream s2) = ProducerConsumerStream.Create();

            // Note, this doesn't handle hangs properly and should be reworked
            if (cert != null)
            {
                var t1 = SslHelper.GetClientStream(s1);
                var t2 = SslHelper.GetServerStream(s2, cert);
                await Task.WhenAll(t1, t2);

                s1 = t1.Result;
                s2 = t2.Result;
            }

            var clientHandler = new ClientHandler(s1, messageSize);
            var serverHandler = new ServerHandler(s2);

            return (clientHandler, serverHandler);
        }

        public static ClientHandler[] Run(X509Certificate2 cert, int clientCount, int messageSize)
        {
            var tasks = Enumerable.Range(0, clientCount)
                            .Select(_ => StartOneAsync(cert, messageSize))
                            .ToArray();

            Task.WaitAll(tasks);

            var handlers = tasks.Select(t => t.Result);
            foreach (var h in handlers)
            {
                TaskHelper.SpawnTask(() => h.clientHandler.Run());
                TaskHelper.SpawnTask(() => h.serverHandler.Run());
            }

            return handlers.Select(x => x.clientHandler).ToArray();
        }
    }
}
