using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.Remoting.Messaging;

namespace NetEngineServer {
    /// <summary>
    /// Pool of clients.
    /// </summary>
    public class ClientPool : IEnumerable<Client> {
        private readonly ConcurrentDictionary<int, Client> _clientsId = new ConcurrentDictionary<int, Client>();

        private readonly ConcurrentDictionary<string, Client> _clientsIdentifier =
            new ConcurrentDictionary<string, Client>();

        private static object Lock = new object();

        public bool GenerateIdentifier { get; set; } = true;

        /// <summary>
        /// Add a client to the pool.
        /// </summary>
        /// <param name="client"></param>
        /// <exception cref="Exception"></exception>
        public void Add(Client client) {
            lock (Lock) {
                if (client.Identifier == null) {
                    if (GenerateIdentifier) {
                        client.Identifier = Guid.NewGuid().ToString();
                    } else {
                        throw new Exception("Client has no identifier");
                    }
                }

                _clientsId.TryAdd(client.Id, client);
                _clientsIdentifier.TryAdd(client.Identifier, client);
            }
        }

        /// <summary>
        /// Remove a client from the pool by its id.
        /// </summary>
        /// <param name="id"></param>
        /// <exception cref="Exception"></exception>
        public void Remove(int id) {
            lock (Lock) {
                var a = _clientsId.TryRemove(id, out var c);
                if (!a)
                    throw new Exception("Cannot remove from id");
                var b = _clientsIdentifier.TryRemove(c.Identifier, out _);
                if (b) return;

                // Only to revert
                _clientsId.GetOrAdd(c.Id, c);
                throw new Exception("Cannot remove from identifier"); 
            }
        }


        /// <summary>
        /// Remove a client from the pool by its id.
        /// </summary>
        /// <param name="identifier"></param>
        /// <exception cref="Exception"></exception>
        public void Remove(string identifier) {
            lock (Lock) {
                var a = _clientsIdentifier.TryRemove(identifier, out var c);
                if (!a)
                    throw new Exception("Cannot remove from id");
                var b = _clientsId.TryRemove(c.Id, out _);
                if (b) return;

                // Only to revert
                _clientsId.GetOrAdd(c.Id, c);
                throw new Exception("Cannot remove from identifier");
            }
        }

        /// <summary>
        /// Get a client from its Id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Client Get(int id) {
            lock (Lock) {
                if (!_clientsId.TryGetValue(id, out var val)) {
                    throw new Exception("Cannot retrieve value");
                }

                return val;
            }
        }

        /// <summary>
        /// Get a client from its identifier.
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Client Get(string identifier) {
            lock (Lock) {
                if (!_clientsIdentifier.TryGetValue(identifier, out var val)) {
                    throw new Exception("Cannot retrieve value");
                }

                return val;
            }            
        }

        public Client this[string key] {
            get => Get(key);
            set => _clientsIdentifier[key] = value;
        }

        public Client this[int key] {
            get => Get(key);
            set => _clientsId[key] = value;
        }

        /// <summary>
        /// Get whether a client exists in the pool from its Id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool Contains(int id) {
            return _clientsId.ContainsKey(id);
        }

        /// <summary>
        /// Get whether a client exists in the pool from its identifier.
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public bool Contains(string identifier) {
            return _clientsIdentifier.ContainsKey(identifier);
        }

        /// <summary>
        /// Tries to get a value from the client pool.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        public bool TryGetValue(int id, out Client client) {
            lock (Lock) {
                if (!Contains(id)) {
                    client = null;
                    return false;
                }

                client = Get(id);
                return true;
            }
        }
        
        /// <summary>
        /// Tries to get a value from the client pool.
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        public bool TryGetValue(string identifier, out Client client) {
            lock (Lock) {
                if (!Contains(identifier)) {
                    client = null;
                    return false;
                }

                client = Get(identifier);
                return true;
            }
        }
        
        /// <summary>
        /// Get the client pool size.
        /// </summary>
        public int Count => _clientsId.Count;

        public IEnumerable<int> Ids => _clientsId.Keys;
        public IEnumerable<string> Identifiers => _clientsIdentifier.Keys;

        IEnumerator<Client> IEnumerable<Client>.GetEnumerator() {
            return _clientsId.Values.GetEnumerator();
        }

        public IEnumerator GetEnumerator() {
            return _clientsId.Values.GetEnumerator();
        }
    }
}