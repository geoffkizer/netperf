using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;

namespace SslStreamPerf
{
    // TODO: Move
    internal class ConnectionManager
    {
        private readonly int _messageSize;
        // TODO: Buffer managers

        public ConnectionManager(int messageSize)
        {
            _messageSize = messageSize;
        }

        public int MessageSize => _messageSize;
    }

    internal sealed class ServerListener
    {
        private readonly Socket _listenSocket;
        private readonly X509Certificate2 _certificate;
        private readonly ConnectionManager _connectionManager;

        public ServerListener(IPEndPoint endPoint, X509Certificate2 cert, int messageSize)
        {
            _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(endPoint);
            _listenSocket.Listen(100);

            _certificate = cert;

            _connectionManager = new ConnectionManager(messageSize);
        }

        public IPEndPoint EndPoint => (IPEndPoint)_listenSocket.LocalEndPoint;

        public void Start()
        {
            TaskHelper.SpawnTask(() => RunServer());
        }

        private async Task RunServer()
        {
            while (true)
            {
                Socket accept = await _listenSocket.AcceptAsync();
                TaskHelper.SpawnTask(async () =>
                {
                    accept.NoDelay = true;
                    Stream s = new NetworkStream(accept);

                    if (_certificate != null)
                    {
                        s = await SslHelper.GetServerStream(s, _certificate);
                    }

                    var serverHandler = new ConnectionHandler(_connectionManager, s, isClient: false);
                    await serverHandler.Run();
                });
            }
        }
    }
}
