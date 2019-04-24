using System;
using System.Linq;
using System.Net;
using System.Runtime.Caching;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NetEngineCore;
using NetEngineCore.Messaging;
using NetEngineServer;
using NetEngineServer.Caching;
using NetEngineServer.Filtering;
using NetEngineServerTest.Filters;
using NetEngineServerTest.Handlers;
using NetEngineServerTest.Filters;


namespace NetEngineServerTest {
    internal class Program {
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public static Server Server;

        public static void Toto(object sender, CacheEventArgs e) {
            Console.WriteLine("item removed !" + (string)e.Object);
            Console.WriteLine(((Cache<int, string>) sender).Count);
        }
        
        public static void Main(string[] args) {
            // Instantiate a server with the port 1337
            Server = new Server(1337);
            
            // Adding two handlers
            Server.Dispatcher.AttachHandler(typeof(AuthenticationMessage), new AuthenticationHandler(Server));
            Server.Dispatcher.AttachHandler(typeof(ExampleMessage), new ExampleServerHandler(Server));
            
            // Adding middlewares
            Server.AttachFilter(new ExampleFilter());
            
            // Adding some events
            Server.Stopped += ServerStopped;
            Server.ClientConnected += NewClient;
            Server.ClientDisconnected += LostClient;
            Server.ClientAuthenticated += ClientAuthenticated;
            Server.ClientEvicted += ClientEvicted;
            Server.Starting += ServerStarting;
            Server.Ready += ServerStarted;

            // SSL
            var indexCert = Array.IndexOf(args, "-s");
            if (indexCert > -1) {
                if (args[indexCert + 1] != null) {
                    var certfile = args[indexCert + 1];
                    Server.UseSsl = true;
                    Server.CertificateFile = certfile;
                } else {
                    Console.WriteLine("Error, cert file not specified in arguments.");   
                }
            }

            // Config
            Server.PacketProcessingMode = PacketProcessingMode.Sequential;
            
            // Run the server
            Server.Run(); 

            // To write some commands
            while (true) {
                var line = Console.ReadLine();
                switch (line) {
                    case "stop":
                        Console.WriteLine("Stopping server...");
                        Server.Stop();
                        SpinWait.SpinUntil(() => Server.Running, 2000); 
                        return;
                    case "list":
                        foreach (Client conn in Server.GetClients()) {
                            string auth = conn.Authenticated ? conn.Identifier : "Anonymous";
                            Console.WriteLine($"- {conn.Id} ({conn.Address}) [{auth}]");
                        }

                        break;
                    case var val when new Regex(@"kick ([a-zA-Z0-9*]+)").IsMatch(val):
                        var user = (new Regex(@"kick ([a-zA-Z0-9*]+)").Match(line)).Groups[1].Value;
                        if (user == "*") {
                            Server.ForceDisconnectAll();
                        } else {
                            Server.ForceDisconnectClient(int.Parse(user));
                        }

                        break;
                    default:
                        var m = new ExampleMessage() {
                            Content = line
                        };
                        Server.Broadcast(m);
                        Console.WriteLine($"Message '{line}' sent!");
                        break;
                }
            }
        }
        
        public static void ServerStopped(object sender, EventArgs args) {
            _logger.Info("Server stopped.");
        }
        
        public static void ServerStarting(object sender, EventArgs args) {
            _logger.Info($"Server starting on port {Server.Port}...");
            if (Server.UseSsl) {
                _logger.Info($"Using SSL secure connection.");
            } else {
                _logger.Info($"Warning: this server is not configured to use SSL secure connection.");
            }
        }
        
        public static void ServerStarted(object sender, EventArgs args) {
            _logger.Info($"Server started.");
        }
        
        public static void NewClient(object sender, ClientEventArgs args) {
            _logger.Info($"New client connected ({args.Client.Address}), not yet authenticated.");
        }
        
        public static void ClientAuthenticated(object sender, ClientEventArgs args) {
            _logger.Info($"Client authenticated ({args.Client.Address} - {args.Client.Identifier}).");
        }
        
        public static void LostClient(object sender, ClientEventArgs args) {
            _logger.Info($"A client has gone ({args.Client.Address}).");
        }
        
        public static void ClientEvicted(object sender, ClientEventArgs args) {
            _logger.Info($"A client has been evicted (no auth delay) ({args.Client.Address}).");
        }
    }
}