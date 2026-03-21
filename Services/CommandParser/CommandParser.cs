namespace CommandParser
{
    public static class CommandParser
    {
        public readonly ref struct ParsedCommand
        {
            public readonly ReadOnlySpan<char> Command;
            public readonly ReadOnlySpan<char> Key;
            public readonly ReadOnlySpan<char> Value;

            public ParsedCommand(ReadOnlySpan<char> command, ReadOnlySpan<char> key, ReadOnlySpan<char> value)
            {
                Command = command;
                Key = key;
                Value = value;
            }
        }

        public static ParsedCommand Parse(ReadOnlySpan<char> input)
        {
            input = input.TrimStart();
            if (input.Length == 0)
            {
                return new ParsedCommand(default, default, default);
            }

            // Find first whitespace
            int firstWhitespace = input.IndexOfAny(WhitespaceChars);
            if (firstWhitespace == -1)
            {
                // Only command
                return new ParsedCommand(input, default, default);
            }

            ReadOnlySpan<char> command = input[..firstWhitespace];

            // Skip whitespace after command
            ReadOnlySpan<char> afterCommand = input[firstWhitespace..].TrimStart();
            if (afterCommand.Length == 0)
            {
                // No key
                return new ParsedCommand(command, default, default);
            }

            // Find second whitespace
            int secondWhitespace = afterCommand.IndexOfAny(WhitespaceChars);
            if (secondWhitespace == -1)
            {
                // Only key, no value
                return new ParsedCommand(command, afterCommand, default);
            }

            ReadOnlySpan<char> key = afterCommand[..secondWhitespace];

            // Skip whitespace after key
            ReadOnlySpan<char> afterKey = afterCommand[secondWhitespace..].TrimStart();
            ReadOnlySpan<char> value = afterKey.Length == 0 ? default : afterKey;

            return new ParsedCommand(command, key, value);
        }

        private static readonly char[] WhitespaceChars = [' ', '\t'];
    }
}
