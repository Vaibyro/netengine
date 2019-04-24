using System;
using System.Diagnostics;
using System.Runtime.Remoting.Channels;
using NetEngineClientTest.Handlers;
using NetEngineCore;
using NetEngineCore.Messaging;
using NetEngineServer;
using NetEngineServer.Messaging.Handling;
using NetEngineServerTest.Filters;
using NetEngineServerTest.Handlers;
using Client = NetEngineClient.Client;

namespace NetEngineBench {
    public class ServerBench {
        public Stopwatch Watch = new Stopwatch();
        
        public Server Server { get; } = new Server(1337);

        public int ClientAmount { get; set; } = 10;
        public int MessagePerClient { get; set; } = 1000;

        public void Run() {
            Console.WriteLine("Starting bench...");
            Server.UseSsl = false;
            Server.Dispatcher.AttachHandler(typeof(AuthenticationMessage), new AuthenticationHandler(Server));
            var handler = new BenchMessageHandler(Server);
            handler.BenchFinished += BenchFinished;
            handler.FinishedCount = ClientAmount;
            Server.Dispatcher.AttachHandler(typeof(ExampleMessage), handler);
            Server.PacketProcessingMode = PacketProcessingMode.Sequential;
            Server.Ready += ServerReady;
            Server.Run();
            Watch.Start();
        }

        public void ServerReady(object sender, EventArgs e) {
            for (int i = 0; i < ClientAmount; i++) {
                var client = new Client("localhost", 1337);
                client.Dispatcher.AttachHandler(typeof(ExampleMessage), new ExampleClientHandler(client));
                client.UseSsl = Server.UseSsl;
                client.Ready += ClientReady;
                client.Run();
            }
        }

        public void ClientReady(object sender, EventArgs e) {
            var client = (Client) sender;
            for (int i = 0; i < MessagePerClient; i++) {
                client.Send(new ExampleMessage("lorem ipsum dolor sit amet, this is a test"));
            }
            client.Send(new ExampleMessage("EOF"));
        }

        public void BenchFinished(object sender, EventArgs e) {
            Watch.Stop();
            Console.WriteLine("Time: " + Watch.ElapsedMilliseconds);
            //Server.ForceDisconnectAll();
        }
    }
}