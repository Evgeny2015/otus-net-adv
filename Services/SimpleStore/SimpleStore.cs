namespace SimpleStore
{
    public class SimpleStore
    {
        private readonly Dictionary<string, byte[]> _storage = new();

        public void Set(string key, byte[] value)
        {
            _storage[key] = value;
        }

        public byte[]? Get(string key)
        {
            _storage.TryGetValue(key, out byte[]? value);
            return value;
        }

        public void Delete(string key)
        {
            _storage.Remove(key);
        }
    }
}
