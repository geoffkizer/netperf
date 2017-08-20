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

            Console.WriteLine("About to call GetClientStream");

            var t1 = SslHelper.GetClientStream(s1);

            Console.WriteLine("About to call GetServerStream");

            var t2 = SslHelper.GetServerStream(s2, cert);
            var delay = Task.Delay(10000);


            Console.WriteLine("About to wait on stream tasks with timeout");
            var completed = await Task.WhenAny(Task.WhenAll(t1, t2), delay);
            if (completed == delay)
            {
                Console.WriteLine($"Establishing SSL connection timed out.  Client complete={t1.IsCompleted}, Server complete={t2.IsCompleted}");
            }

            var clientHandler = new ClientHandler(t1.Result, messageSize);
            var serverHandler = new ServerHandler(t2.Result, messageSize);

            return (clientHandler, serverHandler);
        }

        public static ClientHandler[] Start(X509Certificate2 cert, int clientCount, int messageSize)
        {
            var tasks = Enumerable.Range(0, clientCount)
                            .Select(_ => StartOneAsync(cert, messageSize))
                            .ToArray();

            Console.WriteLine("Tasks created, about to wait on them");

            foreach (var t in tasks)
            {
                t.Wait();
            }

            Console.WriteLine("Completed wait for all tasks");

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
