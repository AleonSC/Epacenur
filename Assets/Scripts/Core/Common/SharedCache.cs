using System.Collections.Concurrent;
using System.Threading;

namespace Epacenur.Core
{
    /// A cache the game and a worker thread can use at once (the worker works the world out ahead of you): a
    /// concurrent dictionary that empties itself when it's grown past a limit. Two threads working out the same
    /// thing at once is harmless: the world comes out the same whoever works it out.
    public sealed class SharedCache<TKey, TValue>
    {
        readonly ConcurrentDictionary<TKey, TValue> map = new ConcurrentDictionary<TKey, TValue>();
        readonly int limit;
        int count;

        public SharedCache(int limit = int.MaxValue) { this.limit = limit; }

        public bool TryGetValue(TKey key, out TValue value) => map.TryGetValue(key, out value);

        /// Remembers a value (emptying the cache first if it's full).
        public TValue this[TKey key]
        {
            set
            {
                if (map.TryAdd(key, value))
                    Counted();
                else
                    map[key] = value;
            }
        }

        /// The value already kept for a key, or this one, kept now: everyone asking gets the same one.
        public TValue GetOrAdd(TKey key, TValue value)
        {
            if (map.TryGetValue(key, out var have))
                return have;
            if (map.TryAdd(key, value))
            {
                Counted();
                return value;
            }
            return map.TryGetValue(key, out have) ? have : value;
        }

        void Counted()
        {
            if (Interlocked.Increment(ref count) > limit)
            {
                Interlocked.Exchange(ref count, 0);
                map.Clear();
            }
        }
    }
}
