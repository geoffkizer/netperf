using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.IO;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace SslStreamPerf
{
    internal static class SslHelper
    {
        public static X509Certificate2 LoadCert()
        {
            var certCollection = new X509Certificate2Collection();
            certCollection.Import("contoso.com.pfx", "testcertificate", X509KeyStorageFlags.DefaultKeySet);

            X509Certificate2 certificate = null;
            foreach (X509Certificate2 c in certCollection)
            {
                if (certificate == null && c.HasPrivateKey) certificate = c;
                else c.Dispose();
            }
            return certificate;
        }

        public static async Task<SslStream> GetServerStream(Stream stream, X509Certificate2 certificate)
        {
            var sslStream = new SslStream(stream);
            await sslStream.AuthenticateAsServerAsync(certificate, false, SslProtocols.Tls11 | SslProtocols.Tls12, false);

            return sslStream;
        }

        public static async Task<SslStream> GetClientStream(Stream stream)
        {
            var sslStream = new SslStream(stream, false, (a, b, c, d) => true);
            await sslStream.AuthenticateAsClientAsync("local");

            return sslStream;
        }
    }
}
