using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.Remoting.Messaging;

namespace NetEngineServer {
    public class ClientPool : IEnumerable<Client> {
        private readonly ConcurrentDictionary<int, Client> _clientsId = new ConcurrentDictionary<int, Client>();
        private readonly ConcurrentDictionary<string, Client> _clientsIdentifier = new ConcurrentDictionary<string, Client>();

        public void Add(Client client) {
            _clientsId.TryAdd(client.Id, client);
            _clientsIdentifier.TryAdd(client.Identifier, client);
        }

        public void Remove(int id) {
            var a = _clientsId.TryRemove(id, out var c);
            if(!a)
                throw new Exception("Cannot remove from id");
            var b = _clientsIdentifier.TryRemove(c.Identifier, out _);
            if (b) return;
            
            // Only to revert
            _clientsId.GetOrAdd(c.Id, c);
            throw new Exception("Cannot remove from identifier");
        }

        public Client Get(int id) {
            if (!_clientsId.TryGetValue(id, out var val)) {
                throw new Exception("Cannot retrieve value");
            }

            return val;
        }
        
        public Client this[string key] {
            get => _clientsIdentifier[key];
            set => _clientsIdentifier[key] = value;
        }

        public bool Contains(int id) {
            return _clientsId.ContainsKey(id);
        }

        public bool Contains(string identifier) {
            return _clientsIdentifier.ContainsKey(identifier);
        }

        public int Count => _clientsId.Count;

        public IEnumerable<int> Ids => _clientsId.Keys;
        public IEnumerable<string> Identifiers => _clientsIdentifier.Keys;
        
        public Client this[int key] {
            get => _clientsId[key];
            set => _clientsId[key] = value;
        }

        IEnumerator<Client> IEnumerable<Client>.GetEnumerator() {
            return _clientsId.Values.GetEnumerator();
        }

        public IEnumerator GetEnumerator() {
            return _clientsId.GetEnumerator();
        }
    }
}