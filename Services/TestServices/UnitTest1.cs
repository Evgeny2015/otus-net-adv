using CommandParser;

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
    }
}
