using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ScatterGatherServer
{
    enum ServerMode
    {
        SendMultiple,
        BufferSends,
        GatherSends
    }

    sealed class Server
    {
        private readonly Socket _listenSocket;
        private ReadOnlyMemory<byte> _responseHeader;
        private ReadOnlyMemory<byte> _responseBody;

        private static ReadOnlyMemory<byte> s_requestHeadersEnd = Encoding.UTF8.GetBytes("\r\n\r\n");

        public Server(IPEndPoint endPoint, int contentSize, ServerMode mode)
        {
            _listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listenSocket.Bind(endPoint);

            _responseHeader = Encoding.UTF8.GetBytes($"HTTP/1.1 200 OK\r\nDate: Date: Thu, 01 Apr 2021 01:23:45 GMT\r\nServer: ScatterGatherServer\r\nContent-Length: {contentSize}\r\n\r\n");
            _responseBody = Encoding.UTF8.GetBytes(new string('a', contentSize));
        }

        public IPEndPoint EndPoint => (IPEndPoint)_listenSocket.LocalEndPoint;

        public void Run()
        {
            _listenSocket.Listen();

            while (true)
            {
                Socket socket = _listenSocket.Accept();
                _ = Task.Run(() => HandleConnection(socket));
            }
        }

        private async Task HandleConnection(Socket socket)
        {
            // This buffer is 256K currently (originally was 64K); I'm going to assume that's enough in practice.
            //byte[] buffer = GC.AllocateUninitializedArray<byte>(256 * 1024, pinned: true);

            //Console.WriteLine("Connection received");

            try
            {
                var readBuffer = new Memory<byte>(new byte[8 * 1024]);

                while (true)
                {
                    // Read the request header
                    // We don't actually parse it, we just look for \r\n\r\n to terminate the header.
                    // We assume all requests are GET with no request body.
                    int readLength = 0;
                    while (true)
                    {
                        int bytesRead = await socket.ReceiveAsync(readBuffer.Slice(readLength), SocketFlags.None);

                        if (bytesRead == 0)
                        {
                            // Client sent EOF.
                            if (readLength != 0)
                            {
                                throw new Exception("Unexpected partial request received");
                            }

                            return;
                        }

                        readLength += bytesRead;
                        int offset = readBuffer.Slice(0, readLength).Span.IndexOf(s_requestHeadersEnd.Span);
                        if (offset != -1)
                        {
                            if (offset + s_requestHeadersEnd.Length != readLength)
                            {
                                throw new Exception("Unexpected data received after header end");
                            }

                            // Done reading the request header
                            break;
                        }

                        if (readLength == readBuffer.Length)
                        {
                            throw new Exception($"Request header exceeds buffer size of {readBuffer.Length}");
                        }
                    }

                    // Send the response.
                    // TODO: This is only one variation, the dumb variation
                    await socket.SendAsync(_responseHeader, SocketFlags.None);
                    await socket.SendAsync(_responseBody, SocketFlags.None);
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
            {
                // do nothing.
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception during request processing: {e}");
            }
            finally
            {
                socket.Dispose();
            }
        }
    }
}
