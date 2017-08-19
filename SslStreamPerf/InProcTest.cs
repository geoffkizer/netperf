using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

namespace SslStreamPerf
{
    internal static class InProcTest
    {
        // Avoid compiler warning
        private static void SpawnTask(Func<Task> a)
        {
            Task.Run(a);
        }

        private static async Task<ClientHandler> StartOneAsync(X509Certificate2 cert, int messageSize)
        {
            (var s1, var s2) = ProducerConsumerStream.Create();

            var t1 = SslHelper.GetClientStream(s1);
            var t2 = SslHelper.GetServerStream(s2, cert);
            await Task.WhenAll(t1, t2);

            var clientHandler = new ClientHandler(t1.Result, messageSize);
            SpawnTask(() => clientHandler.Run());

            var serverHandler = new ServerHandler(t2.Result, messageSize);
            SpawnTask(() => serverHandler.Run());

            return clientHandler;
        }

        public static ClientHandler[] Start(X509Certificate2 cert, int clientCount, int messageSize)
        {
            var tasks = new Task<ClientHandler>[clientCount];

            for (int i = 0; i < clientCount; i++)
            {
                tasks[i] = StartOneAsync(cert, messageSize);
            }

            Task.WhenAll(tasks).Wait();

            return tasks.Select(t => t.Result).ToArray();
        }
    }
}
