using NetEngineClient;
using System;
using System.Threading;
using NetEngineClientTest.Handlers;
using NetEngineCore.Messaging;

namespace NetEngineClientTest {
    internal class Program {
        
        private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        public static Client Client;
        
        public static void Main(string[] args) {
            Client = new Client("localhost", 1337);
            
            // Attaching a handler
            Client.Dispatcher.AttachHandler(typeof(ExampleMessage), new ExampleClientHandler(Client));

            // Events
            Client.Ready += ClientStarted;
            Client.ConnectedToServer += ClientConnected;
            Client.Starting += ClientStarting;
            
            var indexCert = Array.IndexOf(args, "-s");
            if (indexCert > -1) {
                if (args[indexCert + 1] != null) {
                    var certfile = args[indexCert + 1];
                    Client.UseSsl = true;
                    Client.CertificateFile = certfile;
                } else {
                    _logger.Error("Error, cert file not specified in arguments.");   
                }
            }
            
            Client.Run();

            // To write some commands
            while (true) {
                var line = Console.ReadLine();
                switch (line) {
                    default:
                        var m = new ExampleMessage {
                            Content = line
                        };
                        Client.Send(m);
                        Console.WriteLine($"Message '{line}' sent!");
                        break;
                }
            } 
        }
        
        public static void ClientStarted(object sender, EventArgs args) {
            _logger.Info($"Client started.");
        }
        
        public static void ClientConnected(object sender, EventArgs args) {
            _logger.Info($"Client connected successfully.");
        }
        
        public static void ClientStarting(object sender, EventArgs args) {
            _logger.Info($"Client starting...");
            if (Client.UseSsl) {
                _logger.Info($"Using SSL secure connection.");
            } else {
                _logger.Info($"Warning: this server is not configured to use SSL secure connection.");
            }
        }
    }
}