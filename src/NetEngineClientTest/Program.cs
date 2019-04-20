using NetEngineClient;
using System;
using System.Threading;
using NetEngineClientTest.Handlers;
using NetEngineCore.Messaging;

namespace NetEngineClientTest {
    internal class Program {
        public static void Main(string[] args) {
            Console.WriteLine("Server launch");
            var client = new Client("localhost", 1337);
            
            // Attaching a handler
            client.Dispatcher.AttachHandler(typeof(ExampleMessage), new ExampleClientHandler(client));
            
            client.Run();

            // To write some commands
            while (true) {
                var line = Console.ReadLine();
                switch (line) {
                    default:
                        var m = new ExampleMessage() {
                            Content = line
                        };
                        client.Send(m);
                        Console.WriteLine($"Message '{line}' sent!");
                        break;
                }
            }
            
        }
    }
}