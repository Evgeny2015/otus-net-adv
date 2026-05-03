using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Create SimpleStore instance
            var store = new SimpleStore.SimpleStore();

            // Create TCP server instance with store
            var server = new TcpServer.TcpServer("127.0.0.1", 8080, store);

            Console.WriteLine("Starting TCP server...");
            Console.WriteLine("Press Ctrl+C to stop the server.");

            // Start server asynchronously
            var serverTask = server.StartAsync();

            // Keep application running
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}