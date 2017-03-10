using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Managed;
using System.Threading.Tasks;

class Test
{
    public static void Main()
    {
        HttpServer server = new HttpServer(new IPEndPoint(IPAddress.Any, 5000), new PlaintextHandler());
        server.Run();

        Console.WriteLine("Running");
        Console.ReadLine();
    }
}
