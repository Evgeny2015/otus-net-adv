namespace SimpleStore
{
    public class SimpleStore : IDisposable
    {
        private readonly Dictionary<string, byte[]> _storage = new();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        // Statistics fields
        private long _setCount = 0;
        private long _getCount = 0;
        private long _deleteCount = 0;

        public void Set(string key, byte[] value)
        {
            _lock.EnterWriteLock();
            try
            {
                _storage[key] = value;
                Interlocked.Increment(ref _setCount);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public byte[]? Get(string key)
        {
            _lock.EnterReadLock();
            try
            {
                _storage.TryGetValue(key, out byte[]? value);
                Interlocked.Increment(ref _getCount);
                return value;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Delete(string key)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_storage.Remove(key))
                {
                    Interlocked.Increment(ref _deleteCount);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public (long setCount, long getCount, long deleteCount) GetStatistics()
        {
            // to ensure thread-safe reading of statistics
            return (Interlocked.Read(ref _setCount),
                    Interlocked.Read(ref _getCount),
                    Interlocked.Read(ref _deleteCount));
        }

        public void Dispose()
        {
            _lock.Dispose();
        }
    }
}
