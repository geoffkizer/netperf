using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading.Tasks;

public sealed class HttpServer
{
    private IPEndPoint _endPoint;

    public HttpServer(IPEndPoint endPoint)
    {
        _endPoint = endPoint;
    }

    public async void Run()
    {
        var cert = SslHelper.LoadCert();

        TcpListener listener = new TcpListener(_endPoint);
        listener.Start();

        Console.WriteLine($"Server running on {_endPoint}");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            if (client == null)
            {
                break;
            }

            SslStream sslStream = await SslHelper.GetServerStream(client.GetStream(), cert);

            var protocol = sslStream.NegotiatedApplicationProtocol;

            if (protocol == SslApplicationProtocol.Http2)
            {
                Console.WriteLine("Accepted HTTP2 connection");

                var handler = new Http2Server(sslStream);
                handler.Run();
            }
            else 
            {
                Console.WriteLine("Accepted HTTP/1.1 connection");

                var handler = new Http11Server(sslStream);
                handler.Run();
            }
        }
    }
}