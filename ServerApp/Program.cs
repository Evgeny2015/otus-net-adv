using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

class Program
{
    // Static ActivitySource and Meter for the application
    public static readonly ActivitySource ActivitySource = new ActivitySource("TcpServer");
    public static readonly Meter Meter = new Meter("TcpServer");

    static async Task Main(string[] args)
    {
        try
        {
            // Configure OpenTelemetry with console exporter
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySource.Name)
                .AddConsoleExporter()
                .Build();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("TcpServer")
                .AddConsoleExporter()
                .Build();

            // Create SimpleStore instance
            var store = new SimpleStore.SimpleStore();

            // Create TCP server instance with store
            var server = new TcpServer.TcpServer("127.0.0.1", 8080, store);

            Console.WriteLine("Starting TCP server...");
            Console.WriteLine("Press Ctrl+C to stop the server.");
            Console.WriteLine("OpenTelemetry configured with console exporter.");

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