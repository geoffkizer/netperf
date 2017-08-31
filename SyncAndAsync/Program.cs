using System;
using System.IO;
using System.Threading.Tasks;

namespace SyncAndAsync
{
    class Program
    {
        interface IStreamHolder
        {
            ValueTask<int> ReadAsync(ArraySegment<byte> buffer);
            Task WriteAsync(ArraySegment<byte> buffer);
        }

        struct StreamSyncHolder : IStreamHolder
        {
            Stream _stream;

            public StreamSyncHolder(Stream stream)
            {
                _stream = stream;
            }

            public ValueTask<int> ReadAsync(ArraySegment<byte> buffer)
            {
                return new ValueTask<int>(_stream.Read(buffer.Array, buffer.Offset, buffer.Count));
            }

            public Task WriteAsync(ArraySegment<byte> buffer)
            {
                _stream.Write(buffer.Array, buffer.Offset, buffer.Count);
                return Task.CompletedTask;
            }
        }

        struct StreamAsyncHolder : IStreamHolder
        {
            Stream _stream;

            public StreamAsyncHolder(Stream stream)
            {
                _stream = stream;
            }

            public ValueTask<int> ReadAsync(ArraySegment<byte> buffer)
            {
                return new ValueTask<int>(_stream.ReadAsync(buffer.Array, buffer.Offset, buffer.Count));
            }

            public Task WriteAsync(ArraySegment<byte> buffer)
            {
                return _stream.WriteAsync(buffer.Array, buffer.Offset, buffer.Count);
            }
        }

        static void DoIt(Stream s)
        {
            DoItInternalAsync(new StreamSyncHolder(s)).GetAwaiter().GetResult();
        }

        static Task DoItAsync(Stream s)
        {
            return DoItInternalAsync(new StreamAsyncHolder(s));
        }

        static async Task DoItInternalAsync<T>(T streamHolder) where T : IStreamHolder
        {
            byte[] buffer = System.Text.Encoding.ASCII.GetBytes("Hello world!\n");

            await streamHolder.WriteAsync(new ArraySegment<byte>(buffer, 0, buffer.Length));
            await streamHolder.WriteAsync(new ArraySegment<byte>(buffer, 0, buffer.Length));
            await streamHolder.WriteAsync(new ArraySegment<byte>(buffer, 0, buffer.Length));
        }

        static void Main(string[] args)
        {
            var s = Console.OpenStandardOutput();

            DoIt(s);
            DoItAsync(s).Wait();
        }
    }
}
