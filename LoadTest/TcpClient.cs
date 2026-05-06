using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LoadTest
{
    public class TcpClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private Socket? _socket;
        private readonly Encoding _encoding = Encoding.UTF8;

        public TcpClient(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task ConnectAsync()
        {
            if (_socket != null && _socket.Connected)
                return;

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await _socket.ConnectAsync(_host, _port);
        }

        public async Task<string> SetAsync(string key, byte[] value)
        {
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Client is not connected");

            string valueStr = _encoding.GetString(value);
            string command = $"SET {key} {valueStr}\r\n";
            byte[] commandBytes = _encoding.GetBytes(command);

            await _socket.SendAsync(new Memory<byte>(commandBytes), SocketFlags.None);

            // Receive response
            var buffer = new byte[1024];
            int received = await _socket.ReceiveAsync(new Memory<byte>(buffer), SocketFlags.None);
            string response = _encoding.GetString(buffer, 0, received);
            return response;
        }

        public async Task<byte[]?> GetAsync(string key)
        {
            if (_socket == null || !_socket.Connected)
                throw new InvalidOperationException("Client is not connected");

            string command = $"GET {key}\r\n";
            byte[] commandBytes = _encoding.GetBytes(command);

            await _socket.SendAsync(new Memory<byte>(commandBytes), SocketFlags.None);

            // Receive response
            var buffer = new byte[1024];
            int received = await _socket.ReceiveAsync(new Memory<byte>(buffer), SocketFlags.None);

            // Check if response is "(nil)" for null
            string response = _encoding.GetString(buffer, 0, received);
            if (response.StartsWith("(nil)"))
                return null;

            // Return the raw bytes (the server sends the value bytes directly for GET)
            var result = new byte[received];
            Array.Copy(buffer, result, received);
            return result;
        }

        public void Disconnect()
        {
            if (_socket != null)
            {
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Close();
                }
                catch
                {
                    // Ignore
                }
                finally
                {
                    _socket.Dispose();
                    _socket = null;
                }
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}