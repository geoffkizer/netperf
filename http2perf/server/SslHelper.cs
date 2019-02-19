using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.IO;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

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

        var options = new SslServerAuthenticationOptions();
        options.ServerCertificate = certificate;
        options.EnabledSslProtocols = SslProtocols.Tls12;
        options.ApplicationProtocols = new List<SslApplicationProtocol>() { SslApplicationProtocol.Http2, SslApplicationProtocol.Http11 };

        await sslStream.AuthenticateAsServerAsync(options, CancellationToken.None);

        return sslStream;
    }
}
