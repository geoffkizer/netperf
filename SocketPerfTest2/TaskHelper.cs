using System;
using System.Threading.Tasks;

namespace SslStreamPerf
{
    internal static class TaskHelper
    {
        // Avoid compiler warning
        public static void SpawnTask(Func<Task> a)
        {
            Task.Run(async () =>
            {
                try
                {
                    await a();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Caught unexpected exception from detached task:");
                    Console.WriteLine($"{e}");
                }
            });
        }
    }
}
