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
        internal NetEngineCore.Networking.Server NetworkingServer;
        private bool _shouldRun = false;
        private ClientPool _clients = new ClientPool();
        private readonly ServerMessageDispatcher _dispatcher;
        private volatile bool _running = false;
        
        private SafeCacheDictionary<Client> _authWaitList = new SafeCacheDictionary<Client>();
        
        // todo: make the authentication list
        
        /// <summary>
        /// Get whether the server is running.
        /// </summary>
        public bool Running => _running;

        /// <summary>
        /// Get or set if the authentication is mandatory to send messages to the server.
        /// </summary>
        public bool AuthenticationMandatory { get; set; } = true;

        public int DefaultAuthTTL => 1000;
        
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
        /// TODO
        /// </summary>
        public bool UseSsl { get; set; }
        
        public HashSet<string> IpWhiteList { get; } = new HashSet<string>();

        public ServerMessageDispatcher Dispatcher => _dispatcher;

        public event EventHandler OnServerReady = delegate { };
        public event EventHandler OnServerStopped = delegate { };
        public event EventHandler OnClientConnected = delegate { };
        public event EventHandler OnClientDisconnected = delegate { };
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="port"></param>
        public Server(int port) {
            Port = port;
            _dispatcher = new ServerMessageDispatcher(this);
        }

        /// <summary>
        /// Run the server.
        /// </summary>
        public void Run() {
            // Verify if server is already running
            if (Running) {
                throw new Exception("Server already running");
            }

            // Start the networking server
            LogInfo($"Starting the server on port {Port}...");
            NetworkingServer = new NetEngineCore.Networking.Server();
            NetworkingServer.Start(Port);

            // Update the server state
            _shouldRun = true;
            _running = NetworkingServer.Active;

            OnServerReady(this, new EventArgs());
            LogInfo($"Server listening for messages.");
            // Warning : this server instance is blocking!
            
            var task = new Task(() => {
                while (_shouldRun) {
                    // Cycle (create a watch to calculate elapsed time)
                    var watch = System.Diagnostics.Stopwatch.StartNew();

                    // Consume all pending messages
                    while (NetworkingServer.GetNextMessage(out Packet packet)) {
                        // Treat message
                        switch (packet.PacketType) {
                            case PacketType.Connection:
                                ProcessPacketConnection(packet);
                                break;
                            case PacketType.Data:
                                ProcessPacketData(packet);
                                break;
                            case PacketType.Disconnection:
                                ProcessPacketDisconnection(packet);
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
        /// Get one connection.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Client GetClient(int id) {
            return _clients[id];
        }

        /// <summary>
        /// Get all the connections.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Client> GetClients() {
            return _clients.ToList();
        }

        public void Broadcast(Message message) {
            foreach (Client client in _clients) {
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
        /// Process a connection packet.
        /// </summary>
        /// <param name="packet"></param>
        private void ProcessPacketConnection(Packet packet) {
            var connection = new Client(packet.ConnectionId,
                NetworkingServer.GetClientAddress(packet.ConnectionId), this);
            LogInfo($"Client {connection.ToString()} connected (not yet authenticated).");

            // Add the client to the auth waitlist
            //_authWaitList.Add(packet.ConnectionId.ToString(), connection, TimeSpan.FromMilliseconds(DefaultAuthTTL));
            
            _clients.Add(connection); // todo
            OnClientConnected(this, new EventArgs()); // todo: ClientEventArgs
        }

        /// <summary>
        /// Process a disconnection packet.
        /// </summary>
        /// <param name="packet"></param>
        private void ProcessPacketDisconnection(Packet packet) {
            var connection = _clients[packet.ConnectionId];
            LogInfo($"Client {connection.ToString()} disconnected.");
            _clients.Remove(packet.ConnectionId);
            OnClientDisconnected(this, new EventArgs()); // todo: ClientEventArgs
        }

        /// <summary>
        /// Process a data packet.
        /// </summary>
        /// <param name="packet"></param>
        private void ProcessPacketData(Packet packet) {
            LogInfo($"Client {_clients[packet.ConnectionId].Address} sent message");
            var message = MessagePackSerializer.Deserialize<Message>(packet.Data);
            message.ConnectionId = packet.ConnectionId;
            _dispatcher.Dispatch(message);
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Stop() {
            if (!Running) {
                throw new Exception("Server not running");
            }

            _shouldRun = false;
            NetworkingServer.Stop();
            _running = false;
            OnServerStopped(this, new EventArgs());
            LogInfo("Server stopped.");
        }
    }
}