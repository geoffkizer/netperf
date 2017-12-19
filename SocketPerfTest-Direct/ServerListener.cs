﻿using System;
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
        private static BoundIOThreads s_boundIOThreads;

        private static async Task RunServer(Socket listen, string serverType)
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
                        s_customThreadPool.Bind(accept);

                        var serverHandler = new CustomThreadServerHandler(accept);
                        serverHandler.Run();
                    }
                    else if (serverType == "threadbound")
                    {
                        s_boundIOThreads.Bind(accept);

                        var serverHandler = new CustomThreadServerHandler(accept);
                        serverHandler.Run();
                    }
                });
            }
        }

        public static IPEndPoint Run(IPEndPoint endPoint, string serverType, int batchSize)
        {
            if (serverType == "customthreads")
            {
                s_customThreadPool = new CustomThreadPool(batchSize);
            }
            else if (serverType == "threadbound")
            {
                s_boundIOThreads = new BoundIOThreads(batchSize);
            }

            Socket listen = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listen.Bind(endPoint);
            listen.Listen(100);

            TaskHelper.SpawnTask(() => RunServer(listen, serverType));

            return (IPEndPoint)listen.LocalEndPoint;
        }
    }
}
