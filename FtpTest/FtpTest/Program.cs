using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace FtpTest
{
    class Program
    {
        private const string absoluteUri = "ftp://localhost/";


        static void Bug1()
        {
            string resource = absoluteUri + "LargeFile";
            FtpWebRequest r1 = (FtpWebRequest)WebRequest.Create(resource);
            r1.Method = WebRequestMethods.Ftp.DownloadFile;
            FtpWebResponse resp1 = (FtpWebResponse)r1.GetResponse();
            resp1.Close();
        }

        static void PrintException(string name, Action a)
        {
            try
            {
                a();
                Console.WriteLine("{0}: No exception", name);
            }
            catch (Exception e)
            {
                Console.WriteLine("{0}: Exception: {1}", name, e);
            }
        }

        static void DnsError()
        {
            FtpWebRequest r1 = (FtpWebRequest)WebRequest.Create("ftp://nosuchhost.nosuchhost");
            r1.Method = WebRequestMethods.Ftp.DownloadFile;
            FtpWebResponse resp1 = (FtpWebResponse)r1.GetResponse();
            resp1.Close();
        }

        static void ConnectError()
        {
            FtpWebRequest r1 = (FtpWebRequest)WebRequest.Create("ftp://192.168.123.123");
            r1.Method = WebRequestMethods.Ftp.DownloadFile;
            FtpWebResponse resp1 = (FtpWebResponse)r1.GetResponse();
            resp1.Close();
        }

        static void RawDnsError()
        {
            TcpClient t = new TcpClient();
            t.Connect("nosuchhost.nosuchhost", 21);
        }

        static void RawConnectError()
        {
            TcpClient t = new TcpClient();
            t.Connect(IPAddress.Parse("192.168.123.123"), 21);
        }

        static void Stuff()
        {
            FtpWebRequest r = (FtpWebRequest)WebRequest.Create("ftp://foo/");
            Console.WriteLine("CachePolicy = {0}", r.CachePolicy);

            FtpWebRequest r2 = (FtpWebRequest)WebRequest.Create("ftps://foo/");

            PrintException("DnsError", DnsError);
            PrintException("ConnectError", ConnectError);

            PrintException("RawDnsError", RawDnsError);
            PrintException("RawConnectError", RawConnectError);
        }

        static void AsyncNonPassive()
        {
            string resource = "ftp://localhost/ReadOnlyResource";
            FtpWebRequest r1 = (FtpWebRequest)WebRequest.Create(resource);
            r1.UsePassive = false;
            r1.Method = WebRequestMethods.Ftp.DownloadFile;

            var mem = new MemoryStream();
            Task t = r1.GetResponseAsync().ContinueWith(t1 =>
            {
                FtpWebResponse resp1 = (FtpWebResponse)t1.Result;

                resp1.GetResponseStream().CopyToAsync(mem);
                resp1.Close();
            });

            t.Wait();

            if (t.IsFaulted)
                throw t.Exception;
        }

        static void Main(string[] args)
        {
            AsyncNonPassive();
        }
    }
}

