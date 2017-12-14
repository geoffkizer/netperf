using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace SocketPerfTest
{
    internal static class ServerListener
    {
        private static CustomThreadPool s_customThreadPool;

        private static async Task RunServer(Socket listen, string serverType, int batchSize)
        {
            while (true)
            {
                Socket accept = await listen.AcceptAsync();
                TaskHelper.SpawnTask(() =>
                {
                    accept.NoDelay = true;

                    if (serverType == "clrthreads")
                    {
                        var serverHandler = new ClrThreadServerHandler(accept);
                        serverHandler.Run();
                    }
                    else if (serverType == "customthreads")
                    {
                        var serverHandler = new CustomThreadServerHandler(s_customThreadPool, accept);
                        serverHandler.Run();
                    }
                    else
                    { 
                        // TODO
                    }
                });
            }
        }

        public static IPEndPoint Run(IPEndPoint endPoint, string serverType, int batchSize)
        {
            if (serverType == "customthreads")
            {
                s_customThreadPool = new CustomThreadPool();
            }

            Socket listen = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listen.Bind(endPoint);
            listen.Listen(100);

            TaskHelper.SpawnTask(() => RunServer(listen, serverType, batchSize));

            return (IPEndPoint)listen.LocalEndPoint;
        }
    }
}
