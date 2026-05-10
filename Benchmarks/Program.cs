using System;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using SimpleStore;

namespace BinarySerializerBenchmarks
{
    [MemoryDiagnoser]
    public class SerializationBenchmarks
    {
        private UserProfile _testProfile;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _testProfile = new UserProfile
            {
                Id = 42,
                Username = "testuser",
                CreatedAt = DateTime.UtcNow
            };
        }

        [Benchmark(Baseline = true)]
        public byte[] JsonSerialize()
        {
            return JsonSerializer.SerializeToUtf8Bytes(_testProfile);
        }

        [Benchmark]
        public byte[] BinarySerialize()
        {
            using (var memoryStream = new MemoryStream())
            {
                _testProfile.SerializeToBinary(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Running benchmarks...");
            var summary = BenchmarkRunner.Run<SerializationBenchmarks>();
            Console.WriteLine("Benchmarks completed.");
        }
    }
}
