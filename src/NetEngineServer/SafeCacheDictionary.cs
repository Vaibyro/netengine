using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;

namespace NetEngineServer {
    public class SafeCacheDictionary<TValue> : IDictionary<string, TValue> {
        private MemoryCache _cache = new MemoryCache("CachingProvider");

        private static readonly object Padlock = new object();

        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<string, TValue> item) {
            Add(item.Key, item.Value);
        }

        public void Clear() {
            lock (Padlock) {
                _cache.Dispose();
                _cache = new MemoryCache("CachingProvider");
            }
        }

        public bool Contains(KeyValuePair<string, TValue> item) {
            lock (Padlock) {
                return _cache.Contains(item.Key);
            }
        }

        public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, TValue> item) {
            lock (Padlock) {
                return _cache.Remove(item.Key) != null;
            }
        }

        public int Count {
            get {
                lock (Padlock) {
                    // Todo: maybe a bug could occur with the long to int cast
                    return (int) _cache.GetCount();
                }
            }
        }

        public bool IsReadOnly => false; //todo

        public bool ContainsKey(string key) {
            lock (Padlock) {
                return _cache.Contains(key);
            }
        }

        public void Add(string key, TValue value) {
            lock (Padlock) {
                _cache.Add(key, value, DateTimeOffset.MaxValue);
            }
        }

        public void Add(string key, TValue value, DateTime deadline) {
            lock (Padlock) {
                _cache.Add(key, value, deadline);
            }
        }

        public void Add(string key, TValue value, TimeSpan ttl) {
            lock (Padlock) {
                _cache.Add(key, value, DateTime.Now.Add(ttl));
            }
        }

        public bool Remove(string key) {
            lock (Padlock) {
                return _cache.Remove(key) != null;
            }
        }

        public bool TryGetValue(string key, out TValue value) {
            lock (Padlock) {
                value = (TValue) _cache[key];
                return value != null;
            }
        }

        public TValue this[string key] {
            get {
                lock (Padlock) {
                    var res = (TValue) _cache[key];
                    if (res == null) {
                        throw new Exception("CachingProvider-GetItem: Don't contains key: " +
                                            key); //todo: cache exception
                    }

                    return res;
                }
            }
            set {
                lock (Padlock) {
                    _cache[key] = value;
                }
            }
        }

        public ICollection<string> Keys {
            get {
                lock (Padlock) {
                    return _cache.Select(kvp => kvp.Key).ToList();
                }
            }
        }

        public ICollection<TValue> Values {
            get {
                lock (Padlock) {
                    return _cache.Select(kvp => (TValue) kvp.Value).ToList();
                }
            }
        }
    }
}