using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Net.Security;

namespace SslStreamPerf
{
    class Program
    {
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

        static void Main(string[] args)
        {
            SslTest();
//            StreamTest.Run();
        }
    }
}
