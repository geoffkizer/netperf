using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace SslStreamPerf
{
    internal static class InProcTest
    {
        // Avoid compiler warning
        private static void SpawnTask(Func<Task> a)
        {
            Task.Run(a);
        }

        private static async Task<(ClientHandler clientHandler, ServerHandler serverHandler)> StartOneAsync(X509Certificate2 cert, int messageSize)
        {
            (var s1, var s2) = ProducerConsumerStream.Create();

            var t1 = SslHelper.GetClientStream(s1);
            var t2 = SslHelper.GetServerStream(s2, cert);
            await Task.WhenAll(t1, t2);

            var clientHandler = new ClientHandler(t1.Result, messageSize);
            var serverHandler = new ServerHandler(t2.Result, messageSize);

            return (clientHandler, serverHandler);
        }

        public static ClientHandler[] Start(X509Certificate2 cert, int clientCount, int messageSize)
        {
            var tasks = Enumerable.Range(0, clientCount)
                            .Select(_ => StartOneAsync(cert, messageSize))
                            .ToArray();

            Task.WaitAll(tasks);

            var handlers = tasks.Select(t => t.Result);
            foreach (var h in handlers)
            {
                SpawnTask(() => h.clientHandler.Run());
                SpawnTask(() => h.serverHandler.Run());
            }

            return handlers.Select(x => x.clientHandler).ToArray();
        }
    }
}
