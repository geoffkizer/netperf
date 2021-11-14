using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

// TODO: Remove namespace
namespace SslStreamPerf
{
    internal static class SslHelper
    {
        public static X509Certificate2 CreateSelfSignedCert()
        {
            using (RSA rsa = RSA.Create())
            {
                var certReq = new CertificateRequest("CN=contoso.com", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                certReq.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));
                certReq.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

                X509Certificate2 cert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow.AddMonths(1));

                if (OperatingSystem.IsWindows())
                {
                    cert = new X509Certificate2(cert.Export(X509ContentType.Pfx));
                }

                return cert;
            }
        }

        // TODO: Remove these?

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
