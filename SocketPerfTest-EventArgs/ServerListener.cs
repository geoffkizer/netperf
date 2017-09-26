using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace SslStreamPerf
{
    internal static class ServerListener
    {
        private static async Task RunServer(Socket listen)
        {
            while (true)
            {
                Socket accept = await listen.AcceptAsync();
                TaskHelper.SpawnTask(async () =>
                {
                    accept.NoDelay = true;
                    Stream s = new NetworkStream(accept);

                    var serverHandler = new ServerHandler(s);
                    await serverHandler.Run();
                });
            }
        }
    }
}
