using MessagePack;
using NetEngineCore.Messaging;
using NetEngineCore.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using NetEngineServer.Messaging.Dispatching;
using NetEngineServer.Utils;

namespace NetEngineServer {
    /// <summary>
    /// GameServer class.
    /// </summary>
    public class Server {
     
        #region Private members
        
        // Networking server
        internal NetEngineCore.Networking.Server NetworkingServer;
        
        // States
        private bool _shouldRun = false;
        private volatile bool _running = false;
        
        // Dispatcher
        private readonly ServerMessageDispatcher _dispatcher;
        
        // Clients
        private ClientPool _clients = new ClientPool(); // Client pool
        private SafeCacheDictionary<Client> _authWaitList = new SafeCacheDictionary<Client>(); // Not authenticated client pool
        
        #endregion
        
        
        #region Properties
        
        /// <summary>
        /// Get whether the server is running.
        /// </summary>
        public bool Running => _running;

        /// <summary>
        /// Get or set if the authentication is mandatory to send messages to the server.
        /// </summary>
        public bool AuthenticationMandatory { get; set; } = true;

        /// <summary>
        /// Get or set the default auth TTL (default: 1000).
        /// </summary>
        public int DefaultAuthTtl { get; set; } = 1000 * 10; // 10 secs
        
        /// <summary>
        /// Get the server port.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Get or set the maximum frequency of the server.
        /// </summary>
        public int MaxFrequency { get; set; } = 33;

        /// <summary>
        /// Logger for error.
        /// </summary>
        public Action<string> LogError { get; set; } = Console.WriteLine;

        /// <summary>
        /// Logger for info.
        /// </summary>
        public Action<string> LogInfo { get; set; } = Console.WriteLine;

        /// <summary>
        /// Logger for warning.
        /// </summary>
        public Action<string> LogWarning { get; set; } = Console.WriteLine;

        /// <summary>
        /// Auto adapt the frequency with the load.
        /// </summary>
        public bool AdaptiveFrequency { get; set; } = false;

        /// <summary>
        /// Get or set the maximum number of simultaneous connections.
        /// </summary>
        public int MaxConnections { get; set; } = 0;

        /// <summary>
        /// Get the current frequency.
        /// </summary>
        public int CurrentFrequency { get; private set; }

        /// <summary>
        /// Tell the server to use SSL. todo
        /// </summary>
        public bool UseSsl { get; set; }
        
        public string Certificate { get; set; }
        
        /// <summary>
        /// Get a set of whitelisted ips. todo
        /// </summary>
        public HashSet<string> IpWhiteList { get; } = new HashSet<string>();

        /// <summary>
        /// Get the dispatcher.
        /// </summary>
        public ServerMessageDispatcher Dispatcher => _dispatcher;
        
        /// <summary>
        /// Get or set the maximum amount of clients. todo
        /// </summary>
        public int MaxClients { get; set; }
        
        /// <summary>
        /// Get or set the maximum amount of waiting clients (for authentication). todo
        /// </summary>
        public int MaxWaitingClients { get; set; }
        
        #endregion


        #region Events
        public delegate void ClientEventHandler(object sender, ClientEventArgs e);
        public event EventHandler Ready = delegate { };
        public event EventHandler Starting = delegate { };
        public event EventHandler Stopped = delegate { };
        public event EventHandler Stopping = delegate { };
        public event ClientEventHandler ClientConnected = delegate { };
        public event ClientEventHandler ClientDisconnected = delegate { };
        public event ClientEventHandler ClientAuthenticated = delegate { };

        #endregion

        
        #region Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="port"></param>
        public Server(int port) {
            Port = port;
            _dispatcher = new ServerMessageDispatcher(this);
        }

        #endregion
        
        
        #region Public methods

         /// <summary>
        /// Run the server.
        /// </summary>
        public void Run() {
            // Verify if server is already running
            if (Running) {
                throw new Exception("Server already running");
            }

            // Fire event "on server starting..."
            Starting(this, new EventArgs());
            
            // Start the networking server
            NetworkingServer = new NetEngineCore.Networking.Server();
            NetworkingServer.Start(Port);

            // Update the server state
            _shouldRun = true;
            _running = NetworkingServer.Active;

            // Fire event "on server ready..."
            Ready(this, new EventArgs());

            var task = new Task(() => {
                while (_shouldRun) {
                    // Cycle (create a watch to calculate elapsed time)
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    
                    // Consume all pending messages
                    while (NetworkingServer.GetNextMessage(out Packet packet)) {
                        // Treat message
                        switch (packet.PacketType) {
                            case PacketType.Connection:
                                OnConnection(packet);
                                break;
                            case PacketType.Data:
                                OnData(packet);
                                break;
                            case PacketType.Disconnection:
                                OnDisconnection(packet);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }

                    watch.Stop();

                    // Get the current real frequency
                    CurrentFrequency = 1000 / (int) MathUtils.Clamp(watch.ElapsedMilliseconds, (1000 / MaxFrequency));

                    // Sleep
                    Thread.Sleep((1000 / MaxFrequency) -
                                 (int) MathUtils.Clamp(watch.ElapsedMilliseconds, 0, (1000 / MaxFrequency)));
                }
            });
            task.Start();
        }

         /// <summary>
         /// Get one client.
         /// </summary>
         /// <param name="id"></param>
         /// <returns></returns>
         public Client GetClient(int id) {
             return _clients[id];
         }

         /// <summary>
         /// Get one client from the waiting list
         /// </summary>
         /// <param name="id"></param>
         /// <returns></returns>
         public Client GetWaitingListClient(int id) {
             return _authWaitList[id.ToString()];
         }

         /// <summary>
         /// Authenticate a client.
         /// </summary>
         /// <param name="client"></param>
         /// <param name="identifier"></param>
         public void AuthenticateClient(Client client, string identifier) {
             AuthenticateClient(client.Id, identifier);
         }

         /// <summary>
         /// Authenticate a client.
         /// </summary>
         /// <param name="id"></param>
         /// <param name="identifier"></param>
         public void AuthenticateClient(int id, string identifier) {
             var client = _authWaitList[id.ToString()];
             _authWaitList.Remove(id.ToString());
             client.Identifier = identifier;
             client.Authenticated = true;
             _clients.Add(client);
                     
             // Fire event "on client authenticated..."
             ClientAuthenticated(this, new ClientEventArgs(client));
         }
         
         /// <summary>
         /// Get all the clients.
         /// </summary>
         /// <returns></returns>
         public IEnumerable<Client> GetClients() {
             return _clients.ToList();
         }

         /// <summary>
         /// Get all the clients from the waiting list.
         /// </summary>
         /// <returns></returns>
         public IEnumerable<Client> GetWaitingListClients() {
             return _authWaitList.Values;
         }
         
         /// <summary>
         /// Broadcast a message to all clients.
         /// </summary>
         /// <param name="message"></param>
         public void Broadcast(Message message) {
             foreach (Client client in _clients) {
                 client.Send(message);
             }
         }
         
         /// <summary>
         /// Broadcast a message to clients.
         /// </summary>
         /// <param name="targets"></param>
         /// <param name="message"></param>
         public void Broadcast(IEnumerable<Client> targets, Message message) {
             foreach (var client in targets) {
                 client.Send(message);
             }
         }
         
         /// <summary>
         /// Force disconnect a client. This is a disconnection from server, i.e. client will lose connection.
         /// </summary>
         /// <param name="id"></param>
         public void ForceDisconnectClient(int id) {
             if (!_clients.Contains(id) && !NetworkingServer.IsClientConnected(id)) {
                 throw new Exception($"User with id {id} is not connected.");
             }

             _clients.Remove(id);
             NetworkingServer.Disconnect(id);
             LogInfo($"Player with id {id} kicked.");
         }

         /// <summary>
         /// Force disconnect all clients. This is a disconnection from server, i.e. clients will lose connection.
         /// </summary>
         public void ForceDisconnectAll() {
             LogInfo($"Disconnecting every player...");
             if (_clients.Count == 0) {
                 LogInfo($"There is no player to disconnect.");
                 return;
             }

             foreach (var key in _clients.Ids) {
                 ForceDisconnectClient(key);
             }

             LogInfo($"All players were disconnected.");
         }

         /// <summary>
         /// Disconnect a client.
         /// </summary>
         /// <param name="id"></param>
         /// <exception cref="NotImplementedException"></exception>
         public void DisconnectClient(int id) {
             //todo: implement
             throw new NotImplementedException();
         }

         /// <summary>
         /// Disconnect all clients.
         /// </summary>
         /// <exception cref="NotImplementedException"></exception>
         public void DisconnectAll() {
             //todo: implement
             throw new NotImplementedException();
         }
         
         /// <summary>
         /// Stop the server.
         /// </summary>
         public void Stop() {
             if (!Running) {
                 throw new Exception("Server not running");
             }

             // Fire event "on server stopping..."
             Stopping(this, new EventArgs());
             
             _shouldRun = false;
             NetworkingServer.Stop();
             _running = false;
             
             // Fire event "on server stopped..."
             Stopped(this, new EventArgs());
             LogInfo("Server stopped.");
         }
         
        #endregion


        #region Private methods

        /// <summary>
        /// Process a connection packet.
        /// </summary>
        /// <param name="packet"></param>
        private void OnConnection(Packet packet) {
            var client = new Client(packet.ConnectionId,
                NetworkingServer.GetClientAddress(packet.ConnectionId), this);

            // Add the client to the auth waiting list
            _authWaitList.Add(packet.ConnectionId.ToString(), client, TimeSpan.FromMilliseconds(DefaultAuthTtl));
            
            ClientConnected(this, new ClientEventArgs(client));
        }

        /// <summary>
        /// Process a disconnection packet.
        /// </summary>
        /// <param name="packet"></param>
        private void OnDisconnection(Packet packet) {
            var client = _clients[packet.ConnectionId];
            LogInfo($"Client {client.ToString()} disconnected.");
            _clients.Remove(packet.ConnectionId);
            ClientDisconnected(this, new ClientEventArgs(client));
        }

        /// <summary>
        /// Process a data packet.
        /// </summary>
        /// <param name="packet"></param>
        private void OnData(Packet packet) {
            var message = MessagePackSerializer.Deserialize<Message>(packet.Data);
            message.ConnectionId = packet.ConnectionId;
            _dispatcher.Dispatch(message);
        }

        #endregion
        
    }
}