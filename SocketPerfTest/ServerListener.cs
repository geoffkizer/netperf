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
        private static async Task RunServer(Socket listen, X509Certificate2 cert)
        {
            while (true)
            {
                Socket accept = await listen.AcceptAsync();
                TaskHelper.SpawnTask(async () =>
                {
                    Stream s = new NetworkStream(accept);

                    if (cert != null)
                    {
                        s = await SslHelper.GetServerStream(s, cert);
                    }

                    var serverHandler = new ServerHandler(s);
                    await serverHandler.Run();
                });
            }
        }

        // Returns IPEndPoint so that if port was 0 (autoselect), caller can get the port in use.
        public static IPEndPoint Run(IPEndPoint endPoint, X509Certificate2 cert)
        {
            Socket listen = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listen.Bind(endPoint);
            listen.Listen(100);

            TaskHelper.SpawnTask(() => RunServer(listen, cert));

            return (IPEndPoint)listen.LocalEndPoint;
        }
    }
}
