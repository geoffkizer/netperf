using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net.Security;

namespace SslStreamPerf
{
    class StreamTest
    {
        static async Task SendMessage(Stream s, string message, string name)
        {
            Console.WriteLine($"{name}: Sending '{message}'");

            var bytes = Encoding.UTF8.GetBytes(message);
            await s.WriteAsync(bytes, 0, bytes.Length);

            Console.WriteLine($"{name}: Sent");
        }

        static async Task ReceiveMessage(Stream s, string name)
        {
            byte[] readBuffer = new byte[4096];

            Console.WriteLine($"{name}: Awaiting response");

            int bytesRead = await s.ReadAsync(readBuffer, 0, readBuffer.Length);

            Console.WriteLine($"{name}: Received '{Encoding.UTF8.GetString(readBuffer, 0, bytesRead)}'");
        }

        public static async Task RunClient(Stream s)
        {
            using (s)
            {
                await SendMessage(s, "Hello to you!", "Client");
                await ReceiveMessage(s, "Client");
                await SendMessage(s, "Goodbye to you!", "Client");
                await ReceiveMessage(s, "Client");
            }
        }

        public static async Task RunServer(Stream s)
        {
            using (s)
            {
                await ReceiveMessage(s, "Server");
                await SendMessage(s, "Hello to you too!", "Server");
                await ReceiveMessage(s, "Server");
                await SendMessage(s, "Goodbye to you too!", "Server");
            }
        }

        static async Task TestStreams()
        {
            (var s1, var s2) = ProducerConsumerStream.Create();

            var t1 = RunServer(s1);
            await Task.Delay(50);
            var t2 = RunClient(s2);

            await Task.WhenAll(t1, t2);
        }

        static async Task TestStreams2()
        {
            (var s1, var s2) = ProducerConsumerStream.Create();

            var t1 = RunClient(s1);
            await Task.Delay(50);
            var t2 = RunServer(s2);

            await Task.WhenAll(t1, t2);
        }

        public static void Run()
        {
            TestStreams().Wait();
            Console.WriteLine("----------");
            TestStreams2().Wait();
        }

        static void SslTest()
        {
            var cert = SslHelper.LoadCert();

            Stream s1, s2;

            (s1, s2) = ProducerConsumerStream.Create();

            Task<SslStream> t1 = SslHelper.GetClientStream(s1);
            Task<SslStream> t2 = SslHelper.GetServerStream(s2, cert);

            Task.WaitAll(t1, t2);

            s1 = t1.Result;
            s2 = t2.Result;

            Task.WaitAll(StreamTest.RunClient(s1), StreamTest.RunServer(s2));
        }
    }
}
