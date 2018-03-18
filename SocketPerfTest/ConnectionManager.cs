namespace SslStreamPerf
{
    internal class ConnectionManager
    {
        private readonly int _messageSize;
        // TODO: Buffer managers

        public ConnectionManager(int messageSize)
        {
            _messageSize = messageSize;
        }

        public int MessageSize => _messageSize;
    }
}
