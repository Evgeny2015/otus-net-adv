using System;
using System.Text;
using System.Threading.Tasks;
using NBomber.Contracts;
using NBomber.Configuration;
using NBomber.CSharp;
using NBomber.Plugins.Network.Ping;
using SimpleStore;

namespace LoadTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting NBomber load test for TCP server...");

            // Create TCP client step
            static async Task<Response<string>> step()
            {
                try
                {
                    using var client = new TcpClient("127.0.0.1", 8080);
                    await client.ConnectAsync();

                    // Generate random key and UserProfile
                    string key = $"key_{Guid.NewGuid().ToString()[..8]}";
                    var profile = new UserProfile
                    {
                        Id = Guid.NewGuid().GetHashCode(),
                        Username = $"user_{Guid.NewGuid().ToString()[..8]}",
                        CreatedAt = DateTime.UtcNow
                    };

                    // Execute SET operation
                    string response = await client.SetAsync(key, profile);

                    // Check if operation was successful
                    if (response.StartsWith("OK"))
                    {
                        return Response.Ok<string>();
                    }
                    else
                    {
                        return Response.Fail<string>($"Server returned error: {response}", "", 0, 0);
                    }
                }
                catch (Exception ex)
                {
                    return Response.Fail<string>($"Exception: {ex.Message}", "", 0, 0);
                }
            }

            // Create scenario with warm-up and load phases
            var scenario = Scenario.Create("tcp_load_test", async context =>
            {
                await Step.Run("tcp_set_operation", context, step);
                return Response.Ok();
            })
                .WithWarmUpDuration(TimeSpan.FromSeconds(10))
                .WithLoadSimulations(
                    Simulation.Inject(rate: 100,
                                      interval: TimeSpan.FromSeconds(1),
                                      during: TimeSpan.FromSeconds(30))
                );

            // Run the test
            var nodeStats = NBomberRunner
                .RegisterScenarios(scenario)
                .WithWorkerPlugins(new PingPlugin())
                .Run();

            // Print summary
            Console.WriteLine("\n=== Load Test Results ===");
            Console.WriteLine($"Total requests: {nodeStats.AllRequestCount}");
            Console.WriteLine($"Successful: {nodeStats.AllOkCount}");
            Console.WriteLine($"Failed: {nodeStats.AllFailCount}");
            Console.WriteLine($"Requests per second: {nodeStats.AllRequestCount / nodeStats.Duration.TotalSeconds:F2}");

            if (nodeStats.AllFailCount > 0)
            {
                Console.WriteLine("\n=== Error Details ===");
                foreach (var stepStats in nodeStats.ScenarioStats)
                {
                    foreach (var stepStat in stepStats.StepStats)
                    {
                        if (stepStat.Fail.Request.Count > 0)
                        {
                            Console.WriteLine($"Step '{stepStat.StepName}' failures: {stepStat.Fail.Request.Count}");
                            // You could log more details about failures here
                        }
                    }
                }
            }

            Console.WriteLine("\nLoad test completed!");
        }
    }
}
