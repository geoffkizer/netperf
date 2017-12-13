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
        private static async Task RunServer(Socket listen)
        {
            while (true)
            {
                Socket accept = await listen.AcceptAsync();
                TaskHelper.SpawnTask(() =>
                {
                    accept.NoDelay = true;

                    var serverHandler = new ServerHandler(accept);
                    serverHandler.Run();
                });
            }
        }

        public static IPEndPoint Run(IPEndPoint endPoint)
        {
            Socket listen = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listen.Bind(endPoint);
            listen.Listen(100);

            TaskHelper.SpawnTask(() => RunServer(listen));

            return (IPEndPoint)listen.LocalEndPoint;
        }
    }
}
