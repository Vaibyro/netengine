using MessagePack;
using NetEngineCore.Messaging;
using NetEngineCore.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NetEngineServer.Filtering;
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

        // Logging
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        // States
        private bool _shouldRun = false;
        private volatile bool _running = false;

        // Dispatcher
        private readonly ICollection<IFilter> _middlewares;

        // Clients
        private ClientPool _clients = new ClientPool(); // Client pool

        private SafeCacheDictionary<Client>
            _authWaitList = new SafeCacheDictionary<Client>(); // Not authenticated client pool

        // Built-in filters
        private AuthenticationFilter _authenticationFilter = new AuthenticationFilter();

        #endregion


        #region Properties

        /// <summary>
        /// Get whether the server is running.
        /// </summary>
        public bool Running => _running;

        public AuthenticationFilter AuthenticationFilter {
            get => _authenticationFilter;
            set => _authenticationFilter = value;
        }

        /// <summary>
        /// Get or set if the authentication is mandatory to send messages to the server.
        /// </summary>
        public bool UseAuthentication {
            get => _authenticationFilter.Enable;
            set => _authenticationFilter.Enable = value;
        }

        /// <summary>
        /// Get or set the default auth TTL (default: 1000).
        /// </summary>
        public int DefaultAuthTtl { get; set; } = 1000; // 10 secs

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
        public int MaxConnections { get; set; }

        /// <summary>
        /// Get the current frequency.
        /// </summary>
        public int CurrentFrequency { get; private set; }

        /// <summary>
        /// Tell the server to use SSL. todo
        /// </summary>
        public bool UseSsl {
            get => NetworkingServer.UseSsl;
            set => NetworkingServer.UseSsl = value;
        }

        /// <summary>
        /// Get or set the certificate path. todo
        /// </summary>
        public string CertificateFile { get; set; }

        /// <summary>
        /// Get or set whether the server uses whitelist to handle connections. todo
        /// </summary>
        public bool UseWhiteList { get; set; }

        /// <summary>
        /// Get or set whether the server uses whitelist to handle connections. todo
        /// </summary>
        public bool UseBlackList { get; set; }

        /// <summary>
        /// Get a set of whitelisted ips. todo
        /// </summary>
        public HashSet<IPAddress> IpWhiteList { get; } = new HashSet<IPAddress>();

        /// <summary>
        /// Get a set of blacklisted ips. todo
        /// </summary>
        public HashSet<IPAddress> IpBlackList { get; } = new HashSet<IPAddress>();

        /// <summary>
        /// Get the dispatcher.
        /// </summary>
        public ServerMessageDispatcher Dispatcher { get; }

        /// <summary>
        /// Get or set the maximum amount of clients. todo
        /// </summary>
        public int MaxClients { get; set; }

        /// <summary>
        /// Get or set the maximum amount of waiting clients (for authentication). todo
        /// </summary>
        public int MaxWaitingClients { get; set; }

        /// <summary>
        /// Tell the server to use or not filtering logic.
        /// </summary>
        public bool UseFiltering { get; set; } = true;

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
        public event ClientEventHandler ClientEvicted = delegate { };

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="port"></param>
        public Server(int port) {
            Port = port;
            Dispatcher = new ServerMessageDispatcher(this);
            _middlewares = new List<IFilter>();
            _authWaitList.OnCacheRemove = OnAuthWaitingListAutoRemove;
            NetworkingServer = new NetEngineCore.Networking.Server();
            AttachFilter(_authenticationFilter);
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

            // SSL
            if (UseSsl) {
                NetworkingServer.SslCertificate = new X509Certificate2(CertificateFile);
            }
            
            // Start the networking server
            NetworkingServer.Start(Port);

            // Update the server state
            _shouldRun = true;
            _running = NetworkingServer.Active;

            // Fire event "on server ready..."
            Ready(this, new EventArgs());

            var thread = new Thread(() => {
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
            thread.Start();
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

            // Debug log
            _logger.Debug($"Size of the waiting clients list (no auth clients): {_authWaitList.Count}");
            _logger.Debug($"Size of the clients list (auth clients): {_clients.Count}");
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
            if (!NetworkingServer.IsClientConnected(id)) {
                throw new Exception($"User with id {id} is not connected.");
            }

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

        /// <summary>
        /// Attach a middleware.
        /// </summary>
        /// <param name="middleware"></param>
        public void AttachFilter(IFilter middleware) {
            _middlewares.Add(middleware);
        }

        /// <summary>
        /// Detach a middleware.
        /// </summary>
        /// <param name="middleware"></param>
        public void DetachFilter(IFilter middleware) {
            _middlewares.Remove(middleware);
        }

        /// <summary>
        /// Verify if a client is authenticated.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public bool IsAuthenticated(Client client) {
            return IsAuthenticated(client.Id);
        }

        /// <summary>
        /// Verify if a client is authenticated.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool IsAuthenticated(int id) {
            return _authWaitList.ContainsKey(id.ToString());
        }

        #endregion


        #region Private methods

        private void OnAuthWaitingListAutoRemove(CacheEntryRemovedArguments args) {
            var client = (Client) args.CacheItem.Value;
            ForceDisconnectClient(client.Id);
            ClientEvicted(this, new ClientEventArgs(client));
        }

        /// <summary>
        /// Process a connection packet.
        /// </summary>
        /// <param name="packet"></param>
        private void OnConnection(Packet packet) {
            var client = new Client(packet.ConnectionId,
                NetworkingServer.GetClientAddress(packet.ConnectionId), this);

            // Add the client to the auth waiting list
            _authWaitList.Add(packet.ConnectionId.ToString(), client, TimeSpan.FromSeconds(3));

            ClientConnected(this, new ClientEventArgs(client));
        }

        /// <summary>
        /// Process a disconnection packet.
        /// </summary>
        /// <param name="packet"></param>
        private void OnDisconnection(Packet packet) {
            Client client;
            
            // Try removing clients from list
            if (_clients.TryGetValue(packet.ConnectionId, out client)) {
                _clients.Remove(packet.ConnectionId);
                //Console.WriteLine($"Client {client.ToString()} disconnected.");
            }

            // Try removing clients from waiting list
            if (_authWaitList.TryGetValue(packet.ConnectionId.ToString(), out client)) {
                _authWaitList.Remove(packet.ConnectionId.ToString());
                //Console.WriteLine($"Client not authenticated {client.ToString()} disconnected.");
            }

            // If client has been removed (normal disconnection), fire the event
            if (client != null) {
                ClientDisconnected(this, new ClientEventArgs(client));
            }
        }

        /// <summary>
        /// Process a data packet.
        /// </summary>
        /// <param name="packet"></param>
        private void OnData(Packet packet) {
            var message = MessagePackSerializer.Deserialize<Message>(packet.Data);
            message.ConnectionId = packet.ConnectionId;

            // Filter with middleware
            if (UseFiltering) {
                foreach (var middleware in _middlewares) {
                    if (!middleware.Filter(this, message)) {
                        Console.WriteLine("Message from client was not accepted.");
                        return;
                    }
                }
            }

            // Dispatch the packet is filtering is finished
            Dispatcher.Dispatch(message);
        }

        #endregion
    }
}