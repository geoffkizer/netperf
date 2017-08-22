using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace AspNetCore
{
    class Program
    {
        static void Main(string[] args)
        {
            var basePort = 5000;
            var host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Any, basePort); // 80

                    options.Listen(IPAddress.Any, basePort + 1, listenOptions => // 443
                    {
                        listenOptions.UseHttps("testCert.pfx", "testPassword");
                    });
                })
                .UseStartup<Startup>();


            host.Build().Run();
        }

        public class Startup
        {
            public void Configure(IApplicationBuilder app)
            {
                app.UsePlainText();
            }
        }
    }
}
