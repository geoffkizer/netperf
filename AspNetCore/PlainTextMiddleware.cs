using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AspNetCore
{
    public class PlaintextMiddleware
    {
        public static readonly byte[] _helloWorldPayload = Encoding.UTF8.GetBytes("Hello, World!");

        public PlaintextMiddleware(RequestDelegate next)
        {}

        public Task Invoke(HttpContext httpContext)
        {
            return WriteResponse(httpContext.Response);
        }

        public static Task WriteResponse(HttpResponse response)
        {
            var payloadLength = _helloWorldPayload.Length;
            response.StatusCode = 200;
            response.ContentType = "text/plain";
            response.ContentLength = payloadLength;
            return response.Body.WriteAsync(_helloWorldPayload, 0, payloadLength);
        }
    }

    public static class PlaintextMiddlewareExtensions
    {
        public static IApplicationBuilder UsePlainText(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PlaintextMiddleware>();
        }
    }
}