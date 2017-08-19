using System;
using System.Threading;

namespace SslStreamPerf
{
    class Program
    {
        const int clientCount = 16;
        const int messageSize = 2048;
        const int warmupTime = 5;      // Seconds
        const int interval = 3;        // Seconds

        static int GetCurrentRequestCount(ClientHandler[] clientHandlers)
        {
            int total = 0;
            foreach (var c in clientHandlers)
            {
                total += c.RequestCount;
            }

            return total;
        }

        static void ShowResults(ClientHandler[] clientHandlers)
        {
            Console.WriteLine($"Test running");

            Console.WriteLine($"Waiting {warmupTime} seconds for warmup");
            Thread.Sleep(warmupTime * 1000);
            Console.WriteLine("Warmup complete");

            int elapsed = 0;
            int countAfterWarmup = GetCurrentRequestCount(clientHandlers);
            int previousCount = countAfterWarmup;
            while (true)
            {
                Thread.Sleep(interval * 1000);

                elapsed += interval;
                int currentCount = GetCurrentRequestCount(clientHandlers);

                double currentRPS = (currentCount - previousCount) / (double)interval;
                double averageRPS = (currentCount - countAfterWarmup) / (double)elapsed;

                Console.WriteLine($"Elapsed time: {TimeSpan.FromSeconds(elapsed)}    Current RPS: {currentRPS:0.0}    Average RPS: {averageRPS:0.0}");

                previousCount = currentCount;
            }
        }

        static void Main(string[] args)
        {
            var clientHandlers = InProcTest.Start(16, 2048);
            ShowResults(clientHandlers);

//            SslTest();
//            StreamTest.Run();
        }
    }
}
