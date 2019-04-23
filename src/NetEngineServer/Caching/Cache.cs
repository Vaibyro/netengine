using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace NetEngineServer.Caching {
    // Thanks to https://www.codeproject.com/Articles/1033606/Cache-T-A-threadsafe-Simple-Efficient-Generic-In-m
    
    #region Cache<T> class

    
    
    /// <inheritdoc />
    /// <summary>
    /// This is a generic cache subsystem based on key/value pairs, where key is generic, too. Key must be unique.
    /// Every cache entry has its own timeout.
    /// Cache is thread safe and will delete expired entries on its own using System.Threading.Timers (which run on <see cref="T:System.Threading.ThreadPool" /> threads).
    /// </summary>
    public class Cache<TKey, TValue> : IDisposable {
        #region Constructor and class members

        public delegate void CacheEventHandler(object sender, CacheEventArgs e);
        public event CacheEventHandler OnRemove = delegate { };
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Cache{TKey,TValue}"/> class.
        /// </summary>
        public Cache() {
        }

        private Dictionary<TKey, TValue> _cache = new Dictionary<TKey, TValue>();
        private Dictionary<TKey, Timer> _timers = new Dictionary<TKey, Timer>();
        private ReaderWriterLockSlim _locker = new ReaderWriterLockSlim();

        #endregion

        #region IDisposable implementation & Clear

        private bool _disposed = false;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        ///   <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing) {
            if (!_disposed) {
                _disposed = true;

                if (disposing) {
                    // Dispose managed resources.
                    Clear();
                    _locker.Dispose();
                }

                // Dispose unmanaged resources
            }
        }

        /// <summary>
        /// Clears the entire cache and disposes all active timers.
        /// </summary>
        public void Clear() {
            _locker.EnterWriteLock();
            try {
                try {
                    foreach (Timer t in _timers.Values)
                        t.Dispose();
                } catch {
                    // ignored
                }

                _timers.Clear();
                _cache.Clear();
            } finally {
                _locker.ExitWriteLock();
            }
        }

        #endregion

        #region CheckTimer

        // Checks whether a specific timer already exists and adds a new one, if not 
        private void CheckTimer(TKey key, int cacheTimeout, bool restartTimerIfExists) {
            Timer timer;

            if (_timers.TryGetValue(key, out timer)) {
                if (restartTimerIfExists) {
                    timer.Change(
                        (cacheTimeout == Timeout.Infinite ? Timeout.Infinite : cacheTimeout * 1000),
                        Timeout.Infinite);
                }
            } else
                _timers.Add(
                    key,
                    new Timer(
                        new TimerCallback(RemoveByTimer),
                        key,
                        (cacheTimeout == Timeout.Infinite ? Timeout.Infinite : cacheTimeout * 1000),
                        Timeout.Infinite));
        }

        private void RemoveByTimer(object state) {
            var val = Get((TKey) state);
            Remove((TKey) state);
            OnRemove(this, new CacheEventArgs(val));
        }

        #endregion

        #region AddOrUpdate, Get, Remove, Exists, Clear

        /// <summary>
        /// Adds or updates the specified cache-key with the specified cacheObject and applies a specified timeout (in seconds) to this key.
        /// </summary>
        /// <param name="key">The cache-key to add or update.</param>
        /// <param name="cacheObject">The cache object to store.</param>
        /// <param name="cacheTimeout">The cache timeout (lifespan) of this object. Must be 1 or greater.
        /// Specify Timeout.Infinite to keep the entry forever.</param>
        /// <param name="restartTimerIfExists">(Optional). If set to <c>true</c>, the timer for this cacheObject will be reset if the object already
        /// exists in the cache. (Default = false).</param>
        public void AddOrUpdate(TKey key, TValue cacheObject, int cacheTimeout, bool restartTimerIfExists = false) {
            if (_disposed) return;

            if (cacheTimeout != Timeout.Infinite && cacheTimeout < 1)
                throw new ArgumentOutOfRangeException("cacheTimeout must be greater than zero.");

            _locker.EnterWriteLock();
            try {
                CheckTimer(key, cacheTimeout, restartTimerIfExists);

                if (!_cache.ContainsKey(key))
                    _cache.Add(key, cacheObject);
                else
                    _cache[key] = cacheObject;
            } finally {
                _locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds or updates the specified cache-key with the specified cacheObject and applies <c>Timeout.Infinite</c> to this key.
        /// </summary>
        /// <param name="key">The cache-key to add or update.</param>
        /// <param name="cacheObject">The cache object to store.</param>
        public void AddOrUpdate(TKey key, TValue cacheObject) {
            AddOrUpdate(key, cacheObject, Timeout.Infinite);
        }

        /// <summary>
        /// Gets the cache entry with the specified key or returns <c>default(T)</c> if the key is not found.
        /// </summary>
        /// <param name="key">The cache-key to retrieve.</param>
        /// <returns>The object from the cache or <c>default(T)</c>, if not found.</returns>
        public TValue this[TKey key] => Get(key);

        public int Count {
            get {
                _locker.EnterReadLock();
                try {
                    return _cache.Count;
                } finally {
                    _locker.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets the cache entry with the specified key or return <c>default(T)</c> if the key is not found.
        /// </summary>
        /// <param name="key">The cache-key to retrieve.</param>
        /// <returns>The object from the cache or <c>default(T)</c>, if not found.</returns>
        public TValue Get(TKey key) {
            if (_disposed) return default(TValue);

            _locker.EnterReadLock();
            try {
                TValue rv;
                return (_cache.TryGetValue(key, out rv) ? rv : default(TValue));
            } finally {
                _locker.ExitReadLock();
            }
        }

        /// <summary>
        /// Tries to gets the cache entry with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">(out) The value, if found, or <c>default(T)</c>, if not.</param>
        /// <returns><c>True</c>, if <c>key</c> exists, otherwise <c>false</c>.</returns>
        public bool TryGet(TKey key, out TValue value) {
            if (_disposed) {
                value = default(TValue);
                return false;
            }

            _locker.EnterReadLock();
            try {
                return _cache.TryGetValue(key, out value);
            } finally {
                _locker.ExitReadLock();
            }
        }

        /// <summary>
        /// Removes a series of cache entries in a single call for all key that match the specified key pattern.
        /// </summary>
        /// <param name="keyPattern">The key pattern to remove. The Predicate has to return true to get key removed.</param>
        public void Remove(Predicate<TKey> keyPattern) {
            if (_disposed) return;

            _locker.EnterWriteLock();
            try {
                var removers = (from k in _cache.Keys.Cast<TKey>()
                    where keyPattern(k)
                    select k).ToList();

                foreach (TKey workKey in removers) {
                    try {
                        _timers[workKey].Dispose();
                    } catch {
                    }

                    _timers.Remove(workKey);
                    _cache.Remove(workKey);
                }
            } finally {
                _locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes the specified cache entry with the specified key.
        /// If the key is not found, no exception is thrown, the statement is just ignored.
        /// </summary>
        /// <param name="key">The cache-key to remove.</param>
        public void Remove(TKey key) {
            if (_disposed) return;

            _locker.EnterWriteLock();
            try {
                if (_cache.ContainsKey(key)) {
                    try {
                        _timers[key].Dispose();
                    } catch {
                    }

                    _timers.Remove(key);
                    _cache.Remove(key);
                }
            } finally {
                _locker.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks if a specified key exists in the cache.
        /// </summary>
        /// <param name="key">The cache-key to check.</param>
        /// <returns><c>True</c> if the key exists in the cache, otherwise <c>False</c>.</returns>
        public bool Exists(TKey key) {
            if (_disposed) return false;

            _locker.EnterReadLock();
            try {
                return _cache.ContainsKey(key);
            } finally {
                _locker.ExitReadLock();
            }
        }

        #endregion
    }

    #endregion

    #region Other Cache classes (derived)

    /// <summary>
    /// This is a generic cache subsystem based on key/value pairs, where key is a string.
    /// You can add any item to this cache as long as the key is unique, so treat keys as something like namespaces and build them with a 
    /// specific system/syntax in your application.
    /// Every cache entry has its own timeout.
    /// Cache is thread safe and will delete expired entries on its own using System.Threading.Timers (which run on <see cref="ThreadPool"/> threads).
    /// </summary>
    public class Cache<T> : Cache<string, T> {
    }

    /// <summary>
    /// The non-generic Cache class instanciates a Cache{object} that can be used with any type of (mixed) contents.
    /// It also publishes a static <c>.Global</c> member, so a cache can be used even without creating a dedicated instance.
    /// The <c>.Global</c> member is lazy instanciated.
    /// </summary>
    public class Cache : Cache<string, object> {
        #region Static Global Cache instance 

        private static Lazy<Cache> global = new Lazy<Cache>();

        /// <summary>
        /// Gets the global shared cache instance valid for the entire process.
        /// </summary>
        /// <value>
        /// The global shared cache instance.
        /// </value>
        public static Cache Global => global.Value;

        #endregion
    }

    #endregion
}