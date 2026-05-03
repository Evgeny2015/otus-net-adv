using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TcpServer
{
    public class TcpServer
    {
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly SimpleStore.SimpleStore _store;
        private Socket? _serverSocket;

        public TcpServer(IPAddress ipAddress, int port, SimpleStore.SimpleStore store)
        {
            _ipAddress = ipAddress;
            _port = port;
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public TcpServer(string ipAddress, int port, SimpleStore.SimpleStore store) : this(IPAddress.Parse(ipAddress), port, store)
        {
        }

        public async Task StartAsync()
        {
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Bind to local IP address and port
            var localEndPoint = new IPEndPoint(_ipAddress, _port);
            _serverSocket.Bind(localEndPoint);

            // Start listening
            _serverSocket.Listen(backlog: 10);

            Console.WriteLine($"TCP Server started on {_ipAddress}:{_port}");

            // Infinite loop to accept connections
            while (true)
            {
                try
                {
                    var clientSocket = await _serverSocket.AcceptAsync();
                    Console.WriteLine($"Client connected from {clientSocket.RemoteEndPoint}");

                    // Start processing client in separate task
                    _ = ProcessClientAsync(clientSocket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting connection: {ex.Message}");
                }
            }
        }

        private async Task ProcessClientAsync(Socket clientSocket)
        {
            const int bufferSize = 1024;

            try
            {
                while (true)
                {
                    // Rent buffer from ArrayPool
                    var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        // Receive data from client
                        var receiveResult = await clientSocket.ReceiveAsync(
                            new Memory<byte>(buffer),
                            SocketFlags.None);

                        // If received 0 bytes, client has disconnected
                        if (receiveResult == 0)
                        {
                            Console.WriteLine($"Client {clientSocket.RemoteEndPoint} disconnected");
                            break;
                        }

                        // Convert received bytes to string and parse command
                        var receivedData = new ReadOnlySpan<byte>(buffer, 0, receiveResult);
                        var text = System.Text.Encoding.UTF8.GetString(receivedData);

                        // Parse command using CommandParser
                        var parsedCommand = CommandParser.CommandParser.Parse(text.AsSpan());

                        // Output parsed command to console
                        Console.WriteLine($"Parsed command from {clientSocket.RemoteEndPoint}: " +
                                          $"Command='{parsedCommand.Command.ToString()}', " +
                                          $"Key='{parsedCommand.Key.ToString()}', " +
                                          $"Value='{parsedCommand.Value.ToString()}'");

                        // Process command
                        string command = parsedCommand.Command.ToString();
                        string key = parsedCommand.Key.ToString();
                        string valueStr = parsedCommand.Value.ToString();

                        // Convert value from string to byte[] if needed
                        byte[]? valueBytes = string.IsNullOrEmpty(valueStr) ? null : Encoding.UTF8.GetBytes(valueStr);

                        // Process command based on type
                        if (string.IsNullOrEmpty(command))
                        {
                            // Empty command - send error
                            var errorResponse = Encoding.UTF8.GetBytes("-ERR Empty command\r\n");
                            await clientSocket.SendAsync(new Memory<byte>(errorResponse), SocketFlags.None);
                        }
                        else
                        {
                            switch (command.ToUpperInvariant())
                            {
                                case "SET":
                                    if (string.IsNullOrEmpty(key) || valueBytes == null)
                                    {
                                        var errorResponse = Encoding.UTF8.GetBytes("-ERR SET requires key and value\r\n");
                                        await clientSocket.SendAsync(new Memory<byte>(errorResponse), SocketFlags.None);
                                    }
                                    else
                                    {
                                        _store.Set(key, valueBytes);
                                        var okResponse = Encoding.UTF8.GetBytes("OK\r\n");
                                        await clientSocket.SendAsync(new Memory<byte>(okResponse), SocketFlags.None);
                                    }
                                    break;

                                case "GET":
                                    if (string.IsNullOrEmpty(key))
                                    {
                                        var errorResponse = Encoding.UTF8.GetBytes("-ERR GET requires key\r\n");
                                        await clientSocket.SendAsync(new Memory<byte>(errorResponse), SocketFlags.None);
                                    }
                                    else
                                    {
                                        var value = _store.Get(key);
                                        if (value == null)
                                        {
                                            var nilResponse = Encoding.UTF8.GetBytes("(nil)\r\n");
                                            await clientSocket.SendAsync(new Memory<byte>(nilResponse), SocketFlags.None);
                                        }
                                        else
                                        {
                                            await clientSocket.SendAsync(new Memory<byte>(value), SocketFlags.None);
                                        }
                                    }
                                    break;

                                case "DELETE":
                                    if (string.IsNullOrEmpty(key))
                                    {
                                        var errorResponse = Encoding.UTF8.GetBytes("-ERR DELETE requires key\r\n");
                                        await clientSocket.SendAsync(new Memory<byte>(errorResponse), SocketFlags.None);
                                    }
                                    else
                                    {
                                        _store.Delete(key);
                                        var okResponse = Encoding.UTF8.GetBytes("OK\r\n");
                                        await clientSocket.SendAsync(new Memory<byte>(okResponse), SocketFlags.None);
                                    }
                                    break;

                                default:
                                    var unknownResponse = Encoding.UTF8.GetBytes("-ERR Unknown command\r\n");
                                    await clientSocket.SendAsync(new Memory<byte>(unknownResponse), SocketFlags.None);
                                    break;
                            }
                        }
                    }
                    finally
                    {
                        // Return buffer to pool
                        System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket error with client {clientSocket.RemoteEndPoint}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing client {clientSocket.RemoteEndPoint}: {ex.Message}");
            }
            finally
            {
                // Close client socket
                try
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                }
                catch
                {
                    // Ignore errors during cleanup
                }
                finally
                {
                    clientSocket.Dispose();
                }
            }
        }
    }
}