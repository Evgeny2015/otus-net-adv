using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SimpleStore;

namespace TcpServer
{
    public class TcpServer
    {
        // Static ActivitySource and Meter for OpenTelemetry instrumentation
        private static readonly ActivitySource ActivitySource = new("TcpServer");
        private static readonly Meter Meter = new("TcpServer");

        // Metrics instruments
        private static readonly Counter<long> ProcessedCommandsCounter = Meter.CreateCounter<long>("tcp.server.commands.processed", description: "Number of commands processed");
        private static readonly Histogram<double> CommandDurationHistogram = Meter.CreateHistogram<double>("tcp.server.command.duration", unit: "ms", description: "Duration of command processing in milliseconds");

        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly SimpleStore.SimpleStore _store;
        private readonly SemaphoreSlim _connectionSemaphore;
        private Socket? _serverSocket;

        public TcpServer(IPAddress ipAddress, int port, SimpleStore.SimpleStore store, int maxConcurrentConnections = 10)
        {
            _ipAddress = ipAddress;
            _port = port;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _connectionSemaphore = new SemaphoreSlim(maxConcurrentConnections, maxConcurrentConnections);
        }

        public TcpServer(string ipAddress, int port, SimpleStore.SimpleStore store, int maxConcurrentConnections = 10)
            : this(IPAddress.Parse(ipAddress), port, store, maxConcurrentConnections)
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

            Console.WriteLine($"TCP Server started on {_ipAddress}:{_port} (max concurrent connections: {_connectionSemaphore.CurrentCount})");

            // Infinite loop to accept connections
            while (true)
            {
                try
                {
                    // Wait for semaphore before accepting new connection
                    await _connectionSemaphore.WaitAsync();

                    var clientSocket = await _serverSocket.AcceptAsync();
                    Console.WriteLine($"Client connected from {clientSocket.RemoteEndPoint} (active connections: {_connectionSemaphore.CurrentCount})");

                    // Start processing client in separate task
                    _ = ProcessClientAsync(clientSocket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting connection: {ex.Message}");
                    // If we failed after waiting, release the semaphore
                    _connectionSemaphore.Release();
                }
            }
        }

        private async Task ProcessClientAsync(Socket clientSocket)
        {
            const int bufferSize = 1024;
            const int maxMessageSize = 4096; // 4KB limit for memory exhaustion protection
            int totalReceivedBytes = 0;

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

                        // Check for memory exhaustion: if total received bytes exceed limit
                        totalReceivedBytes += receiveResult;
                        if (totalReceivedBytes > maxMessageSize)
                        {
                            Console.WriteLine($"Client {clientSocket.RemoteEndPoint} exceeded message size limit ({totalReceivedBytes} > {maxMessageSize} bytes). Disconnecting.");
                            // Send error message before disconnecting
                            var errorResponse = Encoding.UTF8.GetBytes("-ERR Message too large (max 4KB)\r\n");
                            await clientSocket.SendAsync(new Memory<byte>(errorResponse), SocketFlags.None);
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

                        // Create Activity for tracing
                        using var activity = ActivitySource.StartActivity("ProcessCommand");
                        activity?.SetTag("command.name", command);
                        activity?.SetTag("command.key", key);

                        // Metrics: start stopwatch and prepare to record
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
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
                                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(valueStr))
                                    {
                                        var errorResponse = Encoding.UTF8.GetBytes("-ERR SET requires key and value\r\n");
                                        await clientSocket.SendAsync(new Memory<byte>(errorResponse), SocketFlags.None);
                                    }
                                    else
                                    {
                                        try
                                        {
                                            var profile = JsonSerializer.Deserialize<UserProfile>(valueStr);
                                            if (profile == null)
                                            {
                                                var errorResponse = Encoding.UTF8.GetBytes("-ERR Invalid JSON format\r\n");
                                                await clientSocket.SendAsync(new Memory<byte>(errorResponse), SocketFlags.None);
                                            }
                                            else
                                            {
                                                _store.Set(key, profile);
                                                var okResponse = Encoding.UTF8.GetBytes("OK\r\n");
                                                await clientSocket.SendAsync(new Memory<byte>(okResponse), SocketFlags.None);
                                            }
                                        }
                                        catch (JsonException)
                                        {
                                            var errorResponse = Encoding.UTF8.GetBytes("-ERR Invalid JSON format\r\n");
                                            await clientSocket.SendAsync(new Memory<byte>(errorResponse), SocketFlags.None);
                                        }
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
                                        var profile = _store.Get(key);
                                        if (profile == null)
                                        {
                                            var nilResponse = Encoding.UTF8.GetBytes("(nil)\r\n");
                                            await clientSocket.SendAsync(new Memory<byte>(nilResponse), SocketFlags.None);
                                        }
                                        else
                                        {
                                            var json = JsonSerializer.Serialize(profile);
                                            var jsonBytes = Encoding.UTF8.GetBytes(json);
                                            await clientSocket.SendAsync(new Memory<byte>(jsonBytes), SocketFlags.None);
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
                            // Record metrics
                            stopwatch.Stop();
                            ProcessedCommandsCounter.Add(1, new KeyValuePair<string, object?>("command", command));
                            CommandDurationHistogram.Record(stopwatch.ElapsedMilliseconds, new KeyValuePair<string, object?>("command", command));
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
                // Release the connection semaphore
                _connectionSemaphore.Release();
                Console.WriteLine($"Client {clientSocket.RemoteEndPoint} disconnected (active connections: {_connectionSemaphore.CurrentCount})");

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