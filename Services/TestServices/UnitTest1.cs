using CommandParser;
using SimpleStore;

namespace TestServices
{
    public class UnitTest1
    {
        [Fact]
        public void Parse_ValidInput_ReturnsCorrectComponents()
        {
            // Arrange
            var input = "SET user:1 data";
            // Act
            var parsed = CommandParser.CommandParser.Parse(input);
            // Assert
            Assert.Equal("SET", parsed.Command.ToString());
            Assert.Equal("user:1", parsed.Key.ToString());
            Assert.Equal("data", parsed.Value.ToString());
        }

        [Fact]
        public void Parse_OnlyCommand_ReturnsCommandOnly()
        {
            var input = "GET";
            var parsed = CommandParser.CommandParser.Parse(input);
            Assert.Equal("GET", parsed.Command.ToString());
            Assert.True(parsed.Key.IsEmpty);
            Assert.True(parsed.Value.IsEmpty);
        }

        [Fact]
        public void Parse_CommandAndKey_ReturnsCommandAndKey()
        {
            var input = "GET key";
            var parsed = CommandParser.CommandParser.Parse(input);
            Assert.Equal("GET", parsed.Command.ToString());
            Assert.Equal("key", parsed.Key.ToString());
            Assert.True(parsed.Value.IsEmpty);
        }

        [Fact]
        public void Parse_MultipleSpaces_HandlesCorrectly()
        {
            var input = "SET   key   value";
            var parsed = CommandParser.CommandParser.Parse(input);
            Assert.Equal("SET", parsed.Command.ToString());
            Assert.Equal("key", parsed.Key.ToString());
            Assert.Equal("value", parsed.Value.ToString());
        }

        [Fact]
        public void Parse_LeadingSpaces_Trims()
        {
            var input = "   SET key value";
            var parsed = CommandParser.CommandParser.Parse(input);
            Assert.Equal("SET", parsed.Command.ToString());
            Assert.Equal("key", parsed.Key.ToString());
            Assert.Equal("value", parsed.Value.ToString());
        }

        [Fact]
        public void Parse_EmptyInput_ReturnsEmpty()
        {
            var input = "";
            var parsed = CommandParser.CommandParser.Parse(input);
            Assert.True(parsed.Command.IsEmpty);
            Assert.True(parsed.Key.IsEmpty);
            Assert.True(parsed.Value.IsEmpty);
        }

        [Fact]
        public void Parse_OnlySpaces_ReturnsEmpty()
        {
            var input = "   ";
            var parsed = CommandParser.CommandParser.Parse(input);
            Assert.True(parsed.Command.IsEmpty);
            Assert.True(parsed.Key.IsEmpty);
            Assert.True(parsed.Value.IsEmpty);
        }

        [Fact]
        public async Task SimpleStore_MultithreadedOperations_DataConsistentAndStatisticsCorrect()
        {
            // Arrange
            using var store = new SimpleStore.SimpleStore();
            const int numTasks = 100;
            const int numKeys = 10;
            var tasks = new List<Task>();
            var random = new Random();

            // Act - launch multiple tasks that perform Set and Get operations
            for (int i = 0; i < numTasks; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    int keyIndex = random.Next(numKeys);
                    string key = $"key{keyIndex}";
                    byte[] value = BitConverter.GetBytes(keyIndex * 100 + random.Next(100));

                    // Randomly choose between Set and Get
                    if (random.Next(2) == 0)
                    {
                        store.Set(key, value);
                    }
                    else
                    {
                        store.Get(key);
                    }
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Get statistics
            var stats = store.GetStatistics();

            // Total operations should equal number of tasks
            Assert.Equal(numTasks, stats.setCount + stats.getCount);

            // Verify that store can retrieve values for all keys
            for (int i = 0; i < numKeys; i++)
            {
                store.Get($"key{i}");
            }

            // Perform concurrent Sets and Gets on same key
            using var store2 = new SimpleStore.SimpleStore();
            const string testKey = "concurrentKey";
            byte[] testValue = new byte[] { 1, 2, 3 };

            var setTask = Task.Run(() => store2.Set(testKey, testValue));
            var getTask = Task.Run(() => store2.Get(testKey));

            await Task.WhenAll(setTask, getTask);

            var retrieved = store2.Get(testKey);
            Assert.NotNull(retrieved);
            Assert.Equal(testValue, retrieved);
        }
    }
}
