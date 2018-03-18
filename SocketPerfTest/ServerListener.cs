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
        private static async Task RunServer(BufferManager bufferManager, Socket listen, X509Certificate2 cert)
        {
            while (true)
            {
                Socket accept = await listen.AcceptAsync();
                TaskHelper.SpawnTask(async () =>
                {
                    accept.NoDelay = true;
                    Stream s = new NetworkStream(accept);

                    if (cert != null)
                    {
                        s = await SslHelper.GetServerStream(s, cert);
                    }

                    var serverHandler = new ServerHandler(bufferManager, s);
                    await serverHandler.Run();
                });
            }
        }

        // Returns IPEndPoint so that if port was 0 (autoselect), caller can get the port in use.
        public static IPEndPoint Run(BufferManager bufferManager, IPEndPoint endPoint, X509Certificate2 cert)
        {
            Socket listen = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listen.Bind(endPoint);
            listen.Listen(100);

            TaskHelper.SpawnTask(() => RunServer(bufferManager, listen, cert));

            return (IPEndPoint)listen.LocalEndPoint;
        }
    }
}
