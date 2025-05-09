using System.Collections.Concurrent;

namespace ImageApi.Helpers
{
    /// <summary>
    /// Tracks daily request counts per API key.
    /// </summary>
    public static class DailyQuotaStore
    {
        // thread-safe counts
        private static readonly ConcurrentDictionary<string, int> _counts
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Increment the counter for this key. 
        /// Returns the new count after increment.
        /// </summary>
        public static int Increment(string apiKey)
        {
            return _counts.AddOrUpdate(apiKey, 1, (_, prev) => prev + 1);
        }

        /// <summary>
        /// Get the current count (0 if none).
        /// </summary>
        public static int GetCount(string apiKey)
            => _counts.TryGetValue(apiKey, out var v) ? v : 0;

        /// <summary>
        /// Reset all counters to zero.
        /// </summary>
        public static void ResetAll()
        {
            _counts.Clear();
        }
    }
}
