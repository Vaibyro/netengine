using NetEngineCore.Messaging;
using NetEngineCore.Networking;
using System;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using NetEngineClient.Messaging.Dispatching;

namespace NetEngineClient {
    /// <summary>
    /// GameClient class.
    /// </summary>
    public class Client {
        private bool _shouldRun = true;
        private readonly NetEngineCore.Networking.Client _client = new NetEngineCore.Networking.Client();
        private readonly ClientMessageDispatcher _dispatcher;

        /// <summary>
        /// Get the address of the server.
        /// </summary>
        public string ServerAddress { get; }

        /// <summary>
        /// Get the port of the server.
        /// </summary>
        public int ServerPort { get; }

        /// <summary>
        /// Client connection to server timeout.
        /// </summary>
        public int Timeout { get; set; } = 10;

        /// <summary>
        /// Get if the client if currently connected to the server.
        /// </summary>
        public bool Connected => _client.Connected;

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

        public ClientMessageDispatcher Dispatcher => _dispatcher;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serverAddress"></param>
        /// <param name="serverPort"></param>
        public Client(string serverAddress, int serverPort) {
            ServerAddress = serverAddress;
            ServerPort = serverPort;
            _dispatcher = new ClientMessageDispatcher(this);
        }

        /// <summary>
        /// Run the client.
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void Run() {
            LogInfo("Launching client...");
            CreateConnection();
            
            LogInfo("Connected to server.");
            LogInfo("Trying to authenticate...");

            // Authenticate the client.
            SendAuthentication("testuser", "pass123");
            
            LogInfo("Authentication packet sent.");

            var thread = new Thread(() => {
                while (_shouldRun) {
                    if (!_client.Connected) {
                        // Maybe the server is suddenly down...
                        throw new Exception("Connection with the server lost");
                    }

                    // get new messages from queue
                    while (_client.GetNextMessage(out Packet packet)) {
                        if (packet.PacketType == PacketType.Data) {
                            ProcessPacketData(packet);
                        }
                    }

                    // Todo: implement the final clock rate
                    Thread.Sleep(20);
                }
            });
            thread.Start();
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

        /// <summary>
        /// Disconnect the client from the server.
        /// </summary>
        public void Disconnect() {
            _shouldRun = false;
            _client.Disconnect();
        }

        private void ProcessPacketData(Packet packet) {
            LogInfo($"Server sent message:");
            var message = MessagePackSerializer.Deserialize<Message>(packet.Data);
            message.ConnectionId = packet.ConnectionId;
            _dispatcher.Dispatch(message);
        }

        /// <summary>
        /// Initiate the connection to the server. // todo: change this, not well implemented
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void CreateConnection() {
            _client.Connect(ServerAddress, ServerPort);
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
        }
    }
}