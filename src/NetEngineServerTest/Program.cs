using System;
using System.Net;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NetEngineCore.Messaging;
using NetEngineServer;
using NetEngineServerTest.Handlers;


namespace NetEngineServerTest {
    internal class Program {    
        public static void Main(string[] args) {
            // Instantiate a server with the port 1337
            var server = new Server(1337);
            
            // Adding two handlers
            server.Dispatcher.AttachHandler(typeof(AuthenticationMessage), new AuthenticationHandler(server));
            server.Dispatcher.AttachHandler(typeof(ExampleMessage), new ExampleServerHandler(server));
            
            // Adding some events
            server.Stopped += ServerStopped;
            server.ClientConnected += NewClient;
            server.ClientDisconnected += LostClient;
            
            // Run the server
            server.Run(); 

            // To write some commands
            while (true) {
                var line = Console.ReadLine();
                switch (line) {
                    case "stop":
                        Console.WriteLine("Stopping server...");
                        server.Stop();
                        SpinWait.SpinUntil(() => server.Running, 2000); 
                        return;
                    case "list":
                        foreach (Client conn in server.GetClients()) {
                            string auth = conn.Authenticated ? conn.Identifier : "Anonymous";
                            Console.WriteLine($"- {conn.Id} ({conn.Address}) [{auth}]");
                        }

                        break;
                    case var val when new Regex(@"kick ([a-zA-Z0-9*]+)").IsMatch(val):
                        var user = (new Regex(@"kick ([a-zA-Z0-9*]+)").Match(line)).Groups[1].Value;
                        if (user == "*") {
                            server.ForceDisconnectAll();
                        } else {
                            server.ForceDisconnectClient(int.Parse(user));
                        }

                        break;
                    default:
                        var m = new ExampleMessage() {
                            Content = line
                        };
                        server.Broadcast(m);
                        Console.WriteLine($"Message '{line}' sent!");
                        break;
                }
            }
        }
        
        public static void ServerStopped(object sender, EventArgs args) {
            Console.WriteLine("[From event firing] Server has been stopped!");
        }
        
        public static void NewClient(object sender, EventArgs args) {
            Console.WriteLine("[From event firing] New client!");
        }
        
        public static void LostClient(object sender, EventArgs args) {
            Console.WriteLine("[From event firing] A client has gone!");
        }
    }
}