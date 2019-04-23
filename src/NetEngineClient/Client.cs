using NetEngineCore.Messaging;
using NetEngineCore.Networking;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using NetEngineClient.Messaging.Dispatching;
using NetEngineCore;
using NetEngineCore.Messaging.Dispatching;

namespace NetEngineClient {
    /// <summary>
    /// GameClient class.
    /// </summary>
    public class Client : INetworkSystem {
        #region Private members

        private bool _shouldRun = true;
        private readonly NetEngineCore.Networking.Client _client = new NetEngineCore.Networking.Client();
        private readonly ClientMessageDispatcher _dispatcher;

        #endregion


        #region Events

        public event EventHandler Ready = delegate { };
        public event EventHandler Starting = delegate { };
        public event EventHandler Stopped = delegate { };
        public event EventHandler Stopping = delegate { };
        public event EventHandler ConnectedToServer = delegate { };

        #endregion


        #region Properties

        /// <summary>
        /// Get the address of the server.
        /// </summary>
        public string ServerAddress { get; }

        /// <summary>
        /// Get the port of the server.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Client connection to server timeout.
        /// </summary>
        public int Timeout { get; set; } = 10;

        /// <summary>
        /// Gets if the client is running.
        /// </summary>
        public bool Running { get; private set; }

        /// <summary>
        /// Get if the client if currently connected to the server.
        /// </summary>
        public bool Connected => _client.Connected;

        /// <summary>
        /// Tell the server to use SSL.
        /// </summary>
        public bool UseSsl {
            get => _client.UseSsl;
            set => _client.UseSsl = value;
        }

        /// <summary>
        /// Get or set the certificate path. todo
        /// </summary>
        public string CertificateFile { get; set; }

        /// <summary>
        /// Error logger.
        /// </summary>
        public Action<string> LogError { get; set; } = Console.WriteLine;

        /// <summary>
        /// Info logger.
        /// </summary>
        public Action<string> LogInfo { get; set; } = Console.WriteLine;

        /// <summary>
        /// Warning logger.
        /// </summary>
        public Action<string> LogWarning { get; set; } = Console.WriteLine;

        /// <summary>
        /// Gets the dispatcher.
        /// </summary>
        public IMessageDispatcher Dispatcher => _dispatcher;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serverAddress"></param>
        /// <param name="port"></param>
        public Client(string serverAddress, int port) {
            ServerAddress = serverAddress;
            Port = port;
            _dispatcher = new ClientMessageDispatcher(this);
        }

        #endregion


        #region Public methods

        /// <summary>
        /// Run the client.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Run() {
            Starting(this, EventArgs.Empty);

            // SSL
            if (UseSsl) {
                _client.SslCertificate = new X509Certificate2(CertificateFile);
            }

            // Connect to the server
            _client.Connect(ServerAddress, Port);
            const int delayRetry = 15;
            var time = 0;
            while (!_client.Connected) {
                if (time < Timeout * 1000) {
                    //logger.Info("Waiting for connection...");
                    Thread.Sleep(delayRetry);
                } else {
                    //logger.Info("timeout reached. aborting.");
                    throw new Exception("Timeout reached.");
                }

                time += delayRetry;
            }

            ConnectedToServer(this, EventArgs.Empty);
            Running = true;

            LogInfo("Connected to server.");
            LogInfo("Trying to authenticate...");

            // Authenticate the client.
            SendAuthentication("azerty", "pass123");

            LogInfo("Authentication packet sent.");
            Ready(this, EventArgs.Empty);
            
            // Start the main loop
            var thread = new Thread(Loop);
            thread.Start();
        }

        /// <summary>
        /// Stops the client.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void Stop() {
            if (!Running) {
                throw new Exception("Server not running");
            }

            // Fire event "on client stopping..."
            Stopping(this, new EventArgs());

            _shouldRun = false;
            _client.Disconnect();
            Running = false;

            // Fire event "on client stopped..."
            Stopped(this, new EventArgs());
        }

        /// <summary>
        /// Authenticate the client on the server.
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public void SendAuthentication(string username, string password) {
            var authMessage = new AuthenticationMessage() {Username = username, Password = password};
            Send(authMessage);
        }

        /// <summary>
        /// Send a message to the server.
        /// </summary>
        /// <param name="message"></param>
        public void Send(Message message) {
            var binaryMessage = MessagePackSerializer.Serialize(message);
            _client.Send(binaryMessage);
        }

        /// <summary>
        /// Send data to the server (unsafe).
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data) {
            _client.Send(data);
        }

        /// <summary>
        /// Send packet to the server (unsafe).
        /// </summary>
        /// <param name="packet"></param>
        public void Send(Packet packet) {
            _client.Send(packet.Data);
        }

        #endregion

        
        #region Private members

        /// <summary>
        /// Start the main loop.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void Loop() {
            while (_shouldRun) {
                if (!_client.Connected) {
                    // Maybe the server is suddenly down...
                    throw new Exception("Connection with the server lost");
                }

                // get new messages from queue
                while (_client.GetNextMessage(out Packet packet)) {
                    if (packet.PacketType == PacketType.Data) {
                        OnData(packet);
                    }
                }

                // Todo: implement the final clock rate
                Thread.Sleep(20);
            }
        }

        /// <summary>
        /// When the client receive data.
        /// </summary>
        /// <param name="packet"></param>
        private void OnData(Packet packet) {
            LogInfo($"Server sent message:");
            var message = MessagePackSerializer.Deserialize<Message>(packet.Data);
            message.ConnectionId = packet.ConnectionId;
            _dispatcher.Dispatch(message);
        }

        #endregion
    }
}